using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Atlases;
using Sundex.Components;
using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using EditorScene.Scenes.Components;
using EditorScene.Scenes.Views;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The dialog a dropped TDW sequence file opens: the three ways to import it in a list (a
///     Faithful track, the suggested one, a Piano Roll track, or a new project in place of the open
///     one), and the picked way's card - a preview of what it makes, three facts from the dry run
///     (<see cref="SequenceImporter.Preview" />), and the name. A click, 1-3 or Up/Down picks a way
///     (<see cref="HandleKey" />). Pure form - the owner imports and closes the modal, then calls
///     <see cref="Release" />. The tree is ImportDialog.snx.xml; the mode swap and the facts are here.
/// </summary>
public sealed class ImportDialog
{
    private static readonly ImportMode[] Modes = [ImportMode.Faithful, ImportMode.Track, ImportMode.Project];

    /// <summary>Longest fact a 412 card holds on one line at 13 px.</summary>
    private const int FactLimit = 60;

    private const int FileNameLimit = 40;

    private readonly FlexPanel[] _facts;
    private readonly Panel _factsSlot;
    private readonly FaithfulPreview _faithful;
    private readonly ProjectPreview _project;
    private readonly UIElement[] _previews;
    private readonly Panel _previewSlot;
    private readonly FlexPanel[] _rows;
    private readonly Label _chip;
    private readonly Panel _chipBox;

    /// <summary>Whether each of <see cref="Modes" /> can import this file - the grid ones can't when it fails.</summary>
    private readonly bool[] _available;

    /// <param name="context">The editor's UI context.</param>
    /// <param name="store">The sound atlases, for the previews' icons.</param>
    /// <param name="fileName">The dropped file's name, extension included.</param>
    /// <param name="sequence">The parsed file.</param>
    /// <param name="preview">The dry run of every mode on it.</param>
    /// <param name="sounds">Every name a sequence may use, mapped to its sound.</param>
    /// <param name="palette">The clip colors, which the previews give each sound in turn.</param>
    /// <param name="projectName">The open project, which a project import replaces.</param>
    /// <param name="dirty">Whether it has unsaved changes, which a project import loses.</param>
    public ImportDialog(UIContext context, AtlasStore store, string fileName, Sequence sequence,
        ImportPreview preview, IReadOnlyDictionary<string, Sound> sounds, IReadOnlyList<Vector4> palette,
        string projectName, bool dirty)
    {
        var component = Markup.Build(context, "Scenes/Dialogs/Import Dialog/ImportDialog.snx.xml");
        Element = component.GetID<FlexPanel>("import-dialog");
        NameInput = component.GetID<TextInput>("name-input");
        ImportButton = component.GetID<Button>("import-button");
        CancelButton = component.GetID<Button>("cancel-button");
        CloseButton = component.GetID<Button>("close-button");
        _previewSlot = component.GetID<Panel>("preview-slot");
        _factsSlot = component.GetID<Panel>("facts-slot");
        _rows =
        [
            component.GetID<FlexPanel>("faithful-row"),
            component.GetID<FlexPanel>("piano-roll-row"),
            component.GetID<FlexPanel>("project-row")
        ];

        // The base name: the label font has no 🗿, and the chip beside it says what the file is.
        component.GetID<Label>("file-label")
            .SetTextContents(FaithfulPalette.Shorten(Path.GetFileNameWithoutExtension(fileName), FileNameLimit));
        component.GetID<Label>("count-label").SetTextContents(
            $"{Count(preview.Events, "event")}, {Count(preview.Sounds, "sound")}.");
        component.GetID<Label>("speed-label").SetTextContents(
            preview.GridError is null ? $"Starts at {preview.StartBpm:0.##} BPM." : "");
        NameInput.Value = Path.GetFileNameWithoutExtension(fileName);

        _faithful = new FaithfulPreview(context, store);
        _faithful.Fill(sounds, preview.FaithfulSounds, sequence.Events.Select(ev => ev.SoundEvent ?? ""));
        _project = new ProjectPreview(context, store, sounds, preview, palette);
        _previews = [_faithful, TrackKindPreview.PianoRoll(context, preview, palette), _project];
        // Nothing the sample set or the action list knows: likely not a sequence at all.
        var notTdw = preview.FaithfulSounds.Count == 0 &&
                     !sequence.Events.Any(ev => ev.SoundEvent?.StartsWith('!') == true);
        _facts =
        [
            Facts(context, FaithfulFacts(preview, notTdw)),
            Facts(context, PianoRollFacts(preview, notTdw)),
            Facts(context, ProjectFacts(preview, projectName, dirty, notTdw))
        ];
        _available = [true, preview.Grid is not null, preview.Grid is not null];

        KeyChip.Add(CancelButton, "Esc");
        KeyChip.Add(ImportButton, "Enter");
        _chipBox = (Panel)ImportButton.Children[0];
        _chip = (Label)_chipBox.Children[0];

        for (var i = 0; i < Modes.Length; i++)
        {
            var mode = Modes[i];
            _rows[i].UpdateCursorOnHover = true;
            _rows[i].OnClick = _ => Mode = mode;
            if (!_available[i]) Unavailable(context, _rows[i]);
        }

        Show(_previewSlot, _previews[0]);
        Show(_factsSlot, _facts[0]);
    }

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

    /// <summary>The new track's or project's name; the file's name to begin with.</summary>
    public TextInput NameInput { get; }

    /// <summary>Imports the picked way; its label names it.</summary>
    public Button ImportButton { get; }

    public Button CancelButton { get; }
    public Button CloseButton { get; }

    /// <summary>
    ///     Whether the picked way can import the file. False for the grid ways when the file has
    ///     nothing a grid can hold: <see cref="ImportButton" /> is disabled then, and the owner's
    ///     Enter does nothing, rather than letting the import fail into an alert.
    /// </summary>
    public bool CanImport => _available[Array.IndexOf(Modes, Mode)];

    /// <summary>The picked way: marks its row, shows its preview and facts, and names the action.</summary>
    public ImportMode Mode
    {
        get;
        set
        {
            if (field == value) return;
            field = value;

            var index = Array.IndexOf(Modes, value);
            for (var i = 0; i < _rows.Length; i++)
            {
                // Swapped rather than modified: a hover override comes from the first class with one.
                _rows[i].SetClass("track-kind-row-idle", i != index);
                _rows[i].SetClass("track-kind-row-selected", i == index);
            }

            Show(_previewSlot, _previews[index]);
            Show(_factsSlot, _facts[index]);

            // Replacing the project is the destructive way: the red fill, which takes a dark
            // label. The fill goes in ahead of key-button, whose layout it would otherwise override.
            var replace = value == ImportMode.Project;
            ImportButton.SetClass("key-button", false);
            ImportButton.SetClass("dialog-button-primary", !replace);
            ImportButton.SetClass("dialog-button-danger", replace);
            ImportButton.SetClass("key-button", true);
            ImportButton.Label.SetClass("dark-label", replace);
            _chip.SetClass("key-chip-label-on-accent", !replace);

            // Last, so the dim label wins over whichever fill's label went on above.
            var can = CanImport;
            ImportButton.Label.SetClass("disabled-label", false);
            ImportButton.Label.SetClass("disabled-label", !can);
            ImportButton.Disabled = !can;
            // A chip promises its key; Enter does nothing while the button can't. Taken out rather
            // than hidden, which would leave its room on the button.
            var chip = _chipBox;
            if (!can && chip.Parent == ImportButton) ImportButton.RemoveChild(chip);
            else if (can && chip.Parent != ImportButton)
            {
                ImportButton.AddChild(chip); // AddChild queues it; the reorder below doesn't
                ImportButton.Children = [chip, .. ImportButton.Children.Where(child => child != chip)];
            }
            ImportButton.Value = value switch
            {
                ImportMode.Track => "Import as Piano Roll",
                ImportMode.Project => "Replace project",
                _ => "Import as Faithful Track"
            };
        }
    } = ImportMode.Faithful;

    /// <summary>
    ///     1-3 pick a way, Up/Down step through them. Only reaches the dialog while no field has
    ///     focus, so typing a name never picks a way.
    /// </summary>
    /// <returns>True for a key the dialog used.</returns>
    public bool HandleKey(KeyboardKeyEventArgs e)
    {
        if (e.Control || e.Alt || e.Command) return false;

        var index = Array.IndexOf(Modes, Mode);
        int? to = e.Key switch
        {
            Keys.D1 or Keys.KeyPad1 => 0,
            Keys.D2 or Keys.KeyPad2 => 1,
            Keys.D3 or Keys.KeyPad3 => 2,
            Keys.Up => Math.Max(0, index - 1),
            Keys.Down => Math.Min(Modes.Length - 1, index + 1),
            _ => null
        };
        if (to is not { } pick) return false;

        Mode = Modes[pick];
        return true;
    }

    /// <summary>Frees the previews' icon batches - nothing else owns them. Call once the modal is closed.</summary>
    public void Release()
    {
        _faithful.Release();
        _project.Release();
    }

    private enum Tone
    {
        Ok,
        Warn,
        Bad,
        Hint
    }

    /// <summary>Leads every way's facts when the file has no sound or action the editor knows.</summary>
    private static readonly (Tone, string) NotTdw = (Tone.Warn, "This doesn't look like a TDW sequence.");

    private static (Tone, string)[] FaithfulFacts(ImportPreview preview, bool notTdw)
    {
        var unknown = preview.FaithfulUnknownSounds.Count;
        return
        [
            notTdw ? NotTdw : (Tone.Ok, "Every sound and action, in the order it plays."),
            (Tone.Ok, "!speed, dividers and combines stay as written."),
            unknown == 0
                ? (Tone.Ok, "One new track, with an instrument per sound.")
                : (Tone.Warn, $"{Count(unknown, "unknown sound")} kept as silent steps: {string.Join(", ", preview.FaithfulUnknownSounds)}")
        ];
    }

    private static (Tone, string)[] PianoRollFacts(ImportPreview preview, bool notTdw)
    {
        if (preview.Grid is not { } grid) return GridFailed(preview, notTdw);

        var actions = grid.IgnoredEvents.Values.Sum();
        var unknown = grid.UnknownSounds.Count;
        return
        [
            (Tone.Ok, "Notes on a bar and beat grid, an instrument per sound."),
            grid.QuantizedNotes == 0
                ? (Tone.Ok, "Every note lands on the grid as written.")
                : (Tone.Warn, $"{Count(grid.QuantizedNotes, "note")} snapped to the nearest step."),
            (actions, unknown) switch
            {
                (0, 0) => (Tone.Ok, "No sound or action is left out."),
                (_, 0) => (Tone.Warn, $"{LeftOut(grid)}: {string.Join(", ", grid.IgnoredEvents.Keys)}"),
                (0, _) => (Tone.Warn, $"{LeftOut(grid)}: {string.Join(", ", grid.UnknownSounds)}"),
                _ => (Tone.Warn, $"{LeftOut(grid)}.")
            }
        ];
    }

    private static (Tone, string)[] ProjectFacts(ImportPreview preview, string projectName, bool dirty, bool notTdw)
    {
        // Nothing to replace with: why, rather than what it would replace.
        if (preview.Grid is null) return GridFailed(preview, notTdw);

        // The name gets what the rest of its line leaves of FactLimit.
        var replaces = dirty
            ? (Tone.Bad, $"Replaces {FaithfulPalette.Shorten(projectName, FactLimit - 36)}. Unsaved changes are lost.")
            : (Tone.Warn, $"Replaces {FaithfulPalette.Shorten(projectName, FactLimit - 10)}.");
        var grid = preview.Grid;

        List<string> losses = [];
        if (grid.QuantizedNotes > 0) losses.Add($"{Count(grid.QuantizedNotes, "note")} snapped");
        if (LeftOut(grid) is { } leftOut) losses.Add(leftOut);
        return
        [
            replaces,
            (Tone.Ok, $"{Count(preview.Tracks.Count, "Piano Roll track")}, one per sound."),
            losses.Count == 0
                ? (Tone.Ok, "Nothing is snapped or left out.")
                : (Tone.Warn, string.Join(", ", losses) + ".")
        ];
    }

    /// <summary>"2 actions and 1 unknown sound left out", or null when the grid keeps every event.</summary>
    private static string? LeftOut(ImportWarnings grid)
    {
        var actions = grid.IgnoredEvents.Values.Sum();
        var unknown = grid.UnknownSounds.Count;
        return (actions, unknown) switch
        {
            (0, 0) => null,
            (_, 0) => $"{Count(actions, "action")} left out",
            (0, _) => $"{Count(unknown, "unknown sound")} left out",
            _ => $"{Count(actions, "action")} and {Count(unknown, "unknown sound")} left out"
        };
    }

    /// <summary>
    ///     A file the grid can't take, which only a Faithful track imports: why (Preview's
    ///     GridError) as the one problem, then what to do instead.
    /// </summary>
    private static (Tone, string)[] GridFailed(ImportPreview preview, bool notTdw) =>
    [
        .. notTdw ? [NotTdw] : Array.Empty<(Tone, string)>(),
        (Tone.Bad, $"Can't go on a grid: {char.ToLowerInvariant(preview.GridError![0])}{preview.GridError[1..]}"),
        (Tone.Hint, "Import it as a Faithful track instead.")
    ];

    /// <summary>
    ///     Dims a way that can't import this file and says so under its name. It can still be
    ///     picked, to read why in its facts.
    /// </summary>
    private static void Unavailable(UIContext context, FlexPanel row)
    {
        var name = (Label)row.Children[^1];
        var icon = row.Children[^2];
        icon.SetClass("track-kind-unavailable", true);
        name.SetClass("track-kind-unavailable", true);
        row.RemoveChild(name);
        row.AddChild(new FlexPanel(context)
        {
            Classes = ["track-kind-name-stack"],
            Children = [name, new Label(context, "Not available for this file") { Classes = ["track-kind-unavailable-note"] }]
        });
    }

    private static FlexPanel Facts(UIContext context, IEnumerable<(Tone Tone, string Text)> facts)
    {
        return new FlexPanel(context)
        {
            Classes = ["track-kind-facts"],
            Children =
            [
                .. facts.Select(UIElement (fact) => new FlexPanel(context)
                {
                    Classes = ["track-kind-fact"],
                    Children =
                    [
                        new Label(context, fact.Tone switch
                        {
                            Tone.Ok => Icons.Check,
                            Tone.Hint => Icons.Info,
                            _ => Icons.TriangleAlert
                        }, Icons.Font)
                        {
                            Classes = fact.Tone switch
                            {
                                Tone.Warn => ["track-kind-fact-icon", "track-kind-fact-warn"],
                                Tone.Bad => ["track-kind-fact-icon", "track-kind-fact-bad"],
                                Tone.Hint => ["track-kind-fact-icon", "track-kind-fact-hint"],
                                _ => ["track-kind-fact-icon"]
                            }
                        },
                        new Label(context, FaithfulPalette.Shorten(fact.Text, FactLimit))
                        {
                            Classes = ["track-kind-fact-label"]
                        }
                    ]
                })
            ]
        };
    }

    private static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";

    /// <summary>Makes <paramref name="part" /> the slot's only child.</summary>
    private static void Show(Panel slot, UIElement part)
    {
        foreach (var child in slot.Children.Where(child => child != part).ToArray()) slot.RemoveChild(child);
        if (!slot.Children.Contains(part)) slot.AddChild(part);
    }
}
