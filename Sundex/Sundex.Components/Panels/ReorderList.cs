using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;

namespace Sundex.Components.Panels;

/// <summary>
///     A flex list whose rows can be dragged into a new order. A row is a child in the flow (a
///     markup <c>&lt;each&gt;</c>'s rows count, regions being transparent). Pressing a row and moving
///     past a few pixels lifts it: it follows the pointer, carries the <c>dragging</c> class, and the
///     row it would land next to carries <c>drop-before</c> or <c>drop-after</c>. Letting go fires
///     <see cref="OnMove" /> with the rows' positions. A press that doesn't move is an ordinary click
///     on whatever was pressed, and a press on a button, a field or a slider inside a row stays theirs.
/// </summary>
[MarkupTag("reorder-list")]
public class ReorderList : FlexPanel
{
    /// <summary>How far the pointer moves before a press becomes a drag.</summary>
    private const float Threshold = 6;

    private UIElement? _row, _pressed, _marked;
    private float _downY;
    private bool _dragging;
    private int _from, _to;

    public ReorderList(UIContext context) : base(context)
    {
        Direction = LayoutDirection.Vertical;
    }

    public override string Tag => "reorder-list";

    /// <summary>A row moved from one position to another, both counted among the rows before the move.</summary>
    public Action<int, int>? OnMove { get; set; }

    /// <summary>Turns dragging off without taking the list apart: a read-only view of the same rows.</summary>
    [NamedSetting("reorderable")]
    public bool Reorderable { get; set; } = true;

    /// <summary>The rows, in order: children in the flow, with regions' children in their place.</summary>
    public List<UIElement> Rows()
    {
        var rows = new List<UIElement>();
        Collect(Children, rows);
        return rows;

        static void Collect(List<UIElement> children, List<UIElement> rows)
        {
            foreach (var child in children)
            {
                if (!child.Visible) continue;
                if (child is Region region) Collect(region.Children, rows);
                else if (child.Position == Placement.Flow) rows.Add(child);
            }
        }
    }

    public override bool HandlePress(float x, float y)
    {
        if (!Reorderable || OnMove == null) return false;
        var rows = Rows();
        var target = Context.HoverTarget;
        UIElement? row = null;
        for (var element = target; element != null && element != this; element = element.Parent)
        {
            if (rows.Contains(element))
            {
                row = element;
                break;
            }

            // A control inside the row keeps its own press.
            if (element is Button or TextInput or Slider or Checkbox) return false;
        }

        if (row == null) return false;
        _row = row;
        _pressed = target;
        _downY = y;
        _dragging = false;
        _from = _to = rows.IndexOf(row);
        return true;
    }

    public override void HandlePointerDrag(float x, float y)
    {
        if (_row == null) return;
        if (!_dragging && Math.Abs(y - _downY) < Threshold) return;
        if (!_dragging)
        {
            _dragging = true;
            _row.SetClass("dragging", true);
            _row.ZIndex = 1;
        }

        _row.TranslateY = y - _downY;
        var rows = Rows();
        _to = Landing(rows, y);
        var marked = _to == _from ? null : rows[_to];
        if (ReferenceEquals(marked, _marked)) return;
        Unmark();
        _marked = marked;
        _marked?.SetClass(_to > _from ? "drop-after" : "drop-before", true);
    }

    /// <summary>Where a row let go at <paramref name="y" /> lands: past every row whose middle it crossed.</summary>
    private int Landing(List<UIElement> rows, float y)
    {
        float Middle(UIElement row) => row.Computed.AbsoluteY - (ReferenceEquals(row, _row) ? row.TranslateY : 0) + row.Computed.Height / 2;
        for (var at = 0; at < _from; at++)
            if (y < Middle(rows[at]))
                return at;
        for (var at = rows.Count - 1; at > _from; at--)
            if (y > Middle(rows[at]))
                return at;
        return _from;
    }

    public override bool HandlePointerRelease(float x, float y)
    {
        var row = _row;
        var pressed = _pressed;
        var dragging = _dragging;
        _row = _pressed = null;
        _dragging = false;
        if (row == null) return false;

        if (!dragging)
        {
            // Not a drag: the click the press would have been, on what was pressed.
            if (pressed != null && pressed.ContainsPoint(x, y))
                for (var element = pressed; element != null; element = element.Parent)
                    if (element.OnClick != null)
                    {
                        if (!element.Disabled) element.OnClick(element);
                        break;
                    }

            return true;
        }

        row.TranslateY = 0;
        row.ZIndex = 0;
        row.SetClass("dragging", false);
        Unmark();
        if (_to != _from) OnMove?.Invoke(_from, _to);
        return true;
    }

    private void Unmark()
    {
        if (_marked == null) return;
        _marked.SetClass("drop-before", false);
        _marked.SetClass("drop-after", false);
        _marked = null;
    }
}
