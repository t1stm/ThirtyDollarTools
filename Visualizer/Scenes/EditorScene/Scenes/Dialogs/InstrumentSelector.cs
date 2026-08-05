using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     Picks the note editor's active instrument: a list of the project's instruments
///     (<see cref="InstrumentRow" />, each with its own edit affordance) plus a
///     "+ New instrument" row. Pure view - the owner (<see cref="EditorInterface" />)
///     decides what "pick" means (set the active instrument, or reassign one note's).
/// </summary>
public sealed class InstrumentSelector : FlexPanel
{
    private readonly ScrollView _list;
    private readonly Button _newRow;

    public InstrumentSelector(UIContext context) : base(context)
    {
        ID = "instrument-selector";

        _list = new ScrollView(context) { ID = "instrument-selector-list" };
        AddChild(_list);

        // Fill and hover both come from menu-row - see TrackListPanel's "+ Add track".
        _newRow = new Button(context, "+ New instrument")
        {
            Classes = ["menu-row"],
            OnClick = _ => OnNew?.Invoke()
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

    /// <summary>Full row rebuild - the instrument list is small by design.</summary>
    public void Fill(IEnumerable<Instrument> instruments)
    {
        foreach (var child in _list.Children.ToArray()) _list.RemoveChild(child);
        foreach (var instrument in instruments)
            _list.AddChild(new InstrumentRow(Context, instrument,
                i => OnPick?.Invoke(i), i => OnEdit?.Invoke(i), i => OnDelete?.Invoke(i)));
        _list.AddChild(_newRow);
    }
}