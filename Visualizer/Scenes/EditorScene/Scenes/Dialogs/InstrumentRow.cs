using Sundex.Components;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     One instrument-list row: up to four of its sounds' icons, its name and sound count, then
///     Preview, Edit and Delete icon-buttons. Clicking anywhere else on it picks the instrument.
///     Pure view - <see cref="InstrumentSelector" /> owns the callbacks and the icons' batch.
/// </summary>
public sealed class InstrumentRow : FlexPanel
{
    private readonly Label _count;
    private readonly SoundImage[] _icons;

    internal InstrumentRow(UIContext context, Instrument instrument, SoundImage[] icons, Action<Instrument> onPick,
        Action<Instrument> onPreview, Action<Instrument> onEdit, Action<Instrument> onDelete) : base(context)
    {
        Instrument = instrument;
        _icons = icons;
        Classes = ["instrument-row", "instrument-row-idle"];
        UpdateCursorOnHover = true;
        OnClick = _ => onPick(instrument);

        var sounds = instrument.Sounds.Count;
        _count = new Label(context, sounds == 1 ? "1 sound" : $"{sounds} sounds") { Classes = ["instrument-row-count"] };

        var preview = IconButton(context, Icons.Play, () => onPreview(instrument));
        preview.Label.Classes.Add("icon-button-play");

        Children =
        [
            new FlexPanel(context)
            {
                Classes = ["instrument-row-pick"],
                Children =
                [
                    new FlexPanel(context) { Classes = ["instrument-row-icons"], Children = [.. icons] },
                    new FlexPanel(context)
                    {
                        Classes = ["instrument-row-text"],
                        Children = [new ClippedLabel(context, instrument.Name) { Classes = ["instrument-row-name"] }, _count]
                    }
                ]
            },
            preview,
            IconButton(context, Icons.Pencil, () => onEdit(instrument)),
            IconButton(context, Icons.Trash2, () => onDelete(instrument)),
            new Panel(context) { Classes = ["instrument-row-tail"] }
        ];
    }

    public Instrument Instrument { get; }

    /// <summary>
    ///     Marks the row as the active instrument (or the one the notes being changed share). Swaps
    ///     the idle fill for the selected one rather than adding a modifier: a hover override is
    ///     taken from the first class that has one.
    /// </summary>
    public bool Selected
    {
        set
        {
            SetClass("instrument-row-idle", !value);
            SetClass("instrument-row-selected", value);
            _count.SetClass("instrument-row-count-selected", value);
        }
    }

    /// <summary>Frees the icons' instances in the list's batch; call when the row is dropped.</summary>
    public void ReleaseIcons()
    {
        foreach (var icon in _icons) icon.Release();
    }

    private static Button IconButton(UIContext context, string icon, Action onClick)
    {
        var button = new Button(context, icon, font: Icons.Font) { Classes = ["icon-button"], OnClick = _ => onClick() };
        button.Label.Classes = ["icon-button-icon"];
        return button;
    }
}
