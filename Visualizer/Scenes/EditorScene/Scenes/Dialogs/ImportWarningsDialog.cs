using EditorScene.Scenes.Components;
using EditorScene.Scenes.Views;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using VisualizerScene.Objects.Playfield;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     What a TDW import didn't carry over (ModalLayer content): the events a piano roll has
///     no place for and the sounds the sample set doesn't know, each drawn as the tile the
///     faithful palette draws it with, and how many notes were snapped to the grid. A Piano
///     Roll track import also offers to redo it as a Faithful Track, which keeps all of that.
///     Pure form - the owner wires the buttons and closes the modal. The tree is
///     ImportWarningsDialog.snx.xml.
/// </summary>
public sealed class ImportWarningsDialog
{
    /// <summary>
    ///     Cells shown per grid; the rest are only counted. Two rows of events - the importer
    ///     only drops the handful of visual actions - but one of sounds, since a sequence
    ///     written for a newer sample set can name dozens this one lacks. With every section
    ///     up that is ~630 px, inside the 720 px minimum window.
    /// </summary>
    private const int MaxEventCells = 8;

    private const int MaxSoundCells = 4;

    /// <summary>Longest caption a cell shows, its count included - an 88 px cell at 11 px text.</summary>
    private const int CaptionLimit = 13;

    private const int FileNameLimit = 24;

    private readonly List<EventCanvas> _tiles = [];

    /// <param name="look">
    ///     The playfield look the tiles draw with. Null leaves the cells as captions only -
    ///     the headless tests have no atlas store to draw from.
    /// </param>
    public ImportWarningsDialog(UIContext context, string fileName, ImportMode mode, ImportWarnings warnings,
        PlayfieldSettings? look = null)
    {
        var component = Markup.Build(context, "Scenes/Dialogs/Import Warnings Dialog/ImportWarningsDialog.snx.xml");
        Element = component.GetID<FlexPanel>("import-warnings-dialog");
        FaithfulButton = component.GetID<Button>("faithful-button");
        KeepButton = component.GetID<Button>("keep-button");

        var shape = mode switch
        {
            ImportMode.Track => "a Piano Roll track",
            ImportMode.Faithful => "a Faithful Track",
            _ => "a project"
        };
        component.GetID<Label>("title-label")
            .SetTextContents($"Imported \"{FaithfulPalette.Shorten(fileName, FileNameLimit)}\" as {shape}");

        Section("ignored-section", warnings.IgnoredEvents.Count > 0, section =>
            Fill(section, component.GetID<FlexPanel>("ignored-grid"),
                warnings.IgnoredEvents.Select(pair => (pair.Key, (int?)pair.Value)), MaxEventCells, look));

        Section("unknown-section", warnings.UnknownSounds.Count > 0, section =>
        {
            // A faithful track keeps the step with nothing in it; the grid has nowhere to put one.
            component.GetID<Label>("unknown-caption").SetTextContents(mode == ImportMode.Faithful
                ? "Not in the sample set, so they play as silent steps."
                : "Not in the sample set, so they were left out.");
            Fill(section, component.GetID<FlexPanel>("unknown-grid"),
                warnings.UnknownSounds.Select(name => (name, (int?)null)), MaxSoundCells, look);
        });

        Section("quantized-section", warnings.QuantizedNotes > 0, _ =>
            component.GetID<Label>("quantized-label").SetTextContents(
                $"{warnings.QuantizedNotes} note{(warnings.QuantizedNotes == 1 ? "" : "s")} moved to the nearest step."));

        // Only what a faithful track would have kept: it has no answer to an unknown sound.
        OffersFaithful = mode == ImportMode.Track && (warnings.IgnoredEvents.Count > 0 || warnings.QuantizedNotes > 0);
        if (OffersFaithful)
        {
            KeepButton.Label.SetTextContents("Keep Piano Roll");
        }
        else
        {
            Element.RemoveChild(component.GetID<FlexPanel>("faithful-footer"));
            ((Panel)FaithfulButton.Parent!).RemoveChild(FaithfulButton);
        }

        return;

        void Section(string id, bool shown, Action<FlexPanel> fill)
        {
            var section = component.GetID<FlexPanel>(id);
            if (shown) fill(section);
            else Element.RemoveChild(section);
        }
    }

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

    /// <summary>Redoes the import as a Faithful Track. Out of the tree unless <see cref="OffersFaithful" />.</summary>
    public Button FaithfulButton { get; }

    public Button KeepButton { get; }

    public bool OffersFaithful { get; }

    /// <summary>Frees the tiles' GL buffers - nothing else owns them. Call once the modal is closed.</summary>
    public void Release()
    {
        foreach (var tile in _tiles) tile.SetEvents([]);
    }

    private void Fill(FlexPanel section, FlexPanel grid, IEnumerable<(string Name, int? Count)> entries,
        int maxCells, PlayfieldSettings? look)
    {
        var all = entries.ToList();
        foreach (var (name, count) in all.Take(maxCells)) grid.AddChild(Cell(grid.Context, name, count, look));
        if (all.Count > maxCells)
            section.AddChild(new Label(grid.Context, $"and {all.Count - maxCells} more") { Classes = ["caption-label"] });
    }

    /// <summary>
    ///     One cell: the name and its count over the tile. The tile is the faithful palette's
    ///     own entry for it - an action's palette event, or a bare sound, which the playfield
    ///     draws as its missing icon when the sample set has no such sound.
    /// </summary>
    private FlexPanel Cell(UIContext context, string name, int? count, PlayfieldSettings? look)
    {
        var countText = count is { } n ? $"x{n}" : null;
        var caption = new FlexPanel(context) { Classes = ["cell-caption"] };
        caption.AddChild(new Label(context,
            FaithfulPalette.Shorten(name, CaptionLimit - (countText is null ? 0 : countText.Length + 1)))
        {
            Classes = ["cell-name"]
        });
        if (countText is not null) caption.AddChild(new Label(context, countText) { Classes = ["cell-count"] });

        var cell = new FlexPanel(context) { Classes = ["warning-cell"], Children = [caption] };
        if (look is null) return cell;

        var tile = new EventCanvas(context, look, FaithfulSizing.SoundSize, FaithfulSizing.Margin)
        {
            PerLine = 1,
            BreakOnDividers = false,
            HighlightHover = false,
            UpdateCursorOnHover = false
        };
        tile.SetEvents([
            FaithfulAction.Find(name)?.PaletteEvent() ??
            new NormalEvent { SoundEvent = name, ValueScale = ValueScale.None }
        ]);
        _tiles.Add(tile);
        cell.AddChild(tile);
        return cell;
    }
}
