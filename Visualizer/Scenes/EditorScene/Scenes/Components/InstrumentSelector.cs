using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Components;

/// <summary>
///     Picks the note editor's active instrument: a list of the project's instruments
///     (<see cref="InstrumentRow" />, each with its own edit affordance) plus a
///     "+ New instrument" row. Pure view — the owner (<see cref="EditorInterface" />)
///     decides what "pick" means (set the active instrument, or reassign one note's).
/// </summary>
public sealed class InstrumentSelector : FlexPanel
{
    private static readonly Vector4 BackgroundColor = new(0.086f, 0.086f, 0.118f, 1f); // #16161e
    private static readonly Vector4 MenuFillColor = new(0.2f, 0.204f, 0.29f, 1f); // #33344a
    private static readonly Vector4 MenuFillHoverColor = new(0.247f, 0.255f, 0.376f, 1f); // #3f4160

    private readonly ScrollView _list;
    private readonly Button _newRow;

    public InstrumentSelector(UIContext context) : base(context)
    {
        Direction = LayoutDirection.Vertical;
        Width = 320;
        Height = 420;
        Padding = 10;
        Background = new ColoredPlane { Color = BackgroundColor };

        _list = new ScrollView(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100),
            Spacing = 4
        };
        AddChild(_list);

        // Subtle-filled button matching the menu bar; hover swaps background RGB only
        // (code-built children get no stylesheet state[], per EditorInterface).
        var newFill = new ColoredPlane { Color = MenuFillColor };
        _newRow = new Button(context, "+ New instrument")
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 36,
            FontSizePx = 14f,
            BorderRadius = 6,
            Background = newFill,
            OnClick = _ => OnNew?.Invoke(),
            OnHoverEnter = _ => newFill.Color = MenuFillHoverColor,
            OnHoverExit = _ => newFill.Color = MenuFillColor
        };
    }

    /// <summary>Fired with the picked instrument.</summary>
    public Action<Instrument>? OnPick { get; set; }

    /// <summary>Fired when a row's "Edit" button is used.</summary>
    public Action<Instrument>? OnEdit { get; set; }

    /// <summary>Fired when a row's "Delete" button is used.</summary>
    public Action<Instrument>? OnDelete { get; set; }

    /// <summary>Fired when "+ New instrument" is clicked.</summary>
    public Action? OnNew { get; set; }

    /// <summary>Full row rebuild — the instrument list is small by design.</summary>
    public void Fill(IEnumerable<Instrument> instruments)
    {
        foreach (var child in _list.Children.ToArray()) _list.RemoveChild(child);
        foreach (var instrument in instruments)
            _list.AddChild(new InstrumentRow(Context, instrument,
                i => OnPick?.Invoke(i), i => OnEdit?.Invoke(i), i => OnDelete?.Invoke(i)));
        _list.AddChild(_newRow);
    }
}
