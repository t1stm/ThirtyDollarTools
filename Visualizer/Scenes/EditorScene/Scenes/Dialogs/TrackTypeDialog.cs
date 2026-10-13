using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Atlases;
using Shared.Renderer.Planes;
using Sundex.Components;
using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     "+ Add track": the three kinds in a list, and the picked one's card - a preview of the
///     track, three facts, and the new track's name and color. A click, 1-3 or Up/Down picks a
///     kind (<see cref="HandleKey" />). Pure form - the owner adds the track and closes the modal.
///     Built once and shown again with <see cref="Reset" />, so the preview's icon batch is made
///     once. The tree is TrackTypeDialog.snx.xml; the kind swap and the swatches are here.
/// </summary>
public sealed class TrackTypeDialog
{
    private static readonly TrackKind[] Kinds = [TrackKind.PianoRoll, TrackKind.Faithful, TrackKind.Wave];

    private readonly Label _check;
    private readonly FaithfulPreview _faithful;
    private readonly FlexPanel[] _facts;
    private readonly Panel _factsSlot;
    private readonly Panel _nameRow;
    private readonly Panel _nameSlot;
    private readonly UIElement[] _previews;
    private readonly Panel _previewSlot;
    private readonly FlexPanel[] _rows;
    private readonly FlexPanel[] _swatches;
    private readonly Panel _waveNameRow;

    /// <param name="context">The editor's UI context.</param>
    /// <param name="store">The sound atlases, for the faithful preview's icons.</param>
    /// <param name="palette">The clip colors a track can take - the arrangement's.</param>
    /// <param name="defaultColor">The fill of a track with no color of its own.</param>
    public TrackTypeDialog(UIContext context, AtlasStore store, IReadOnlyList<Vector4> palette, Vector4 defaultColor)
    {
        var component = Markup.Build(context, "Scenes/Dialogs/Track Type Dialog/TrackTypeDialog.snx.xml");
        Element = component.GetID<FlexPanel>("track-type-dialog");
        NameInput = component.GetID<TextInput>("name-input");
        AddButton = component.GetID<Button>("add-button");
        CancelButton = component.GetID<Button>("cancel-button");
        CloseButton = component.GetID<Button>("close-button");
        _previewSlot = component.GetID<Panel>("preview-slot");
        _factsSlot = component.GetID<Panel>("facts-slot");
        _nameSlot = component.GetID<Panel>("name-slot");
        _nameRow = component.GetID<Panel>("name-row");
        _waveNameRow = component.GetID<Panel>("wave-name-row");
        _rows =
        [
            component.GetID<FlexPanel>("piano-roll-row"),
            component.GetID<FlexPanel>("faithful-row"),
            component.GetID<FlexPanel>("wave-row")
        ];
        _facts =
        [
            component.GetID<FlexPanel>("piano-roll-facts"),
            component.GetID<FlexPanel>("faithful-facts"),
            component.GetID<FlexPanel>("wave-facts")
        ];

        _faithful = new FaithfulPreview(context, store);
        _previews = [TrackKindPreview.PianoRoll(context), _faithful, TrackKindPreview.Wave(context)];

        KeyChip.Add(CancelButton, "Esc");
        KeyChip.Add(AddButton, "Enter");

        for (var i = 0; i < Kinds.Length; i++)
        {
            var kind = Kinds[i];
            _rows[i].UpdateCursorOnHover = true;
            _rows[i].OnClick = _ => Kind = kind;
        }

        // The swatches: the default fill, then the palette - TrackColorDialog's set and order.
        var swatches = component.GetID<FlexPanel>("color-swatches");
        _swatches = [.. new[] { defaultColor }.Concat(palette).Select((color, i) => Swatch(context, color, i))];
        foreach (var swatch in _swatches) swatches.AddChild(swatch);
        _check = new Label(context, Icons.Check, Icons.Font) { Classes = ["track-swatch-check"] };
        _swatches[0].AddChild(_check);

        // The markup holds every kind's parts; keep the Piano Roll's, which Kind starts at.
        Show(_previewSlot, _previews[0]);
        Show(_factsSlot, _facts[0]);
        Show(_nameSlot, _nameRow);
    }

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

    /// <summary>The new track's name. Not used for a wave track, which takes its file's.</summary>
    public TextInput NameInput { get; }

    /// <summary>Adds the picked kind; its label names it.</summary>
    public Button AddButton { get; }

    public Button CancelButton { get; }
    public Button CloseButton { get; }

    /// <summary>The picked kind: marks its row, shows its preview and facts, and names the action.</summary>
    public TrackKind Kind
    {
        get;
        set
        {
            if (field == value) return;
            field = value;

            var index = Array.IndexOf(Kinds, value);
            for (var i = 0; i < _rows.Length; i++)
            {
                // Swapped rather than modified: a hover override comes from the first class with one.
                _rows[i].SetClass("track-kind-row-idle", i != index);
                _rows[i].SetClass("track-kind-row-selected", i == index);
            }

            Show(_previewSlot, _previews[index]);
            Show(_factsSlot, _facts[index]);
            Show(_nameSlot, value == TrackKind.Wave ? _waveNameRow : _nameRow);
            AddButton.Value = value switch
            {
                TrackKind.Faithful => "Add Faithful",
                TrackKind.Wave => "Choose .wav…",
                _ => "Add Piano Roll"
            };
        }
    } = TrackKind.PianoRoll;

    /// <summary>The new track's clip color: an index into the palette, or null for the default fill.</summary>
    public int? ColorIndex
    {
        get;
        set
        {
            field = value;
            ((Panel)_check.Parent!).RemoveChild(_check);
            _swatches[value is { } index ? index + 1 : 0].AddChild(_check);
        }
    }

    /// <summary>
    ///     Back to a fresh form for the next show: Piano Roll, the default color, and the name the
    ///     next track would get. Also fills the faithful preview's icons once the atlases are in.
    /// </summary>
    public void Reset(string name, IReadOnlyDictionary<string, Sound> sounds)
    {
        Kind = TrackKind.PianoRoll;
        ColorIndex = null;
        NameInput.Value = name;
        _faithful.Fill(sounds);
    }

    /// <summary>
    ///     1-3 pick a kind, Up/Down step through them. Only reaches the dialog while no field
    ///     has focus, so typing a name never picks a kind.
    /// </summary>
    /// <returns>True for a key the dialog used.</returns>
    public bool HandleKey(KeyboardKeyEventArgs e)
    {
        if (e.Control || e.Alt || e.Command) return false;

        var index = Array.IndexOf(Kinds, Kind);
        int? to = e.Key switch
        {
            Keys.D1 or Keys.KeyPad1 => 0,
            Keys.D2 or Keys.KeyPad2 => 1,
            Keys.D3 or Keys.KeyPad3 => 2,
            Keys.Up => Math.Max(0, index - 1),
            Keys.Down => Math.Min(Kinds.Length - 1, index + 1),
            _ => null
        };
        if (to is not { } pick) return false;

        Kind = Kinds[pick];
        return true;
    }

    /// <summary>Makes <paramref name="part" /> the slot's only child.</summary>
    private static void Show(Panel slot, UIElement part)
    {
        foreach (var child in slot.Children.Where(child => child != part).ToArray()) slot.RemoveChild(child);
        if (!slot.Children.Contains(part)) slot.AddChild(part);
    }

    private FlexPanel Swatch(UIContext context, Vector4 color, int position)
    {
        // The fill is a palette entry, so it is set here; track-swatch declares no background.
        return new FlexPanel(context)
        {
            Classes = ["track-swatch"],
            Background = new ColoredPlane { Color = color },
            UpdateCursorOnHover = true,
            OnClick = _ => ColorIndex = position == 0 ? null : position - 1
        };
    }
}
