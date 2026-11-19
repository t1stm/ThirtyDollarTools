using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;

namespace Sundex.Components.Panels;

/// <summary>
///     Equal columns, filled row by row: CSS <c>grid-template-columns: repeat(auto-fill, minmax(min, 1fr))</c>
///     with <see cref="MinColumn" />, or a fixed <see cref="Columns" /> count. Each row is as tall as
///     its tallest cell, and every cell is stretched to the row. Gaps, padding, regions and
///     absolute children work as in a <see cref="FlexPanel" />.
/// </summary>
[MarkupTag("grid")]
public class Grid(UIContext context) : FlexPanel(context)
{
    public override string Tag => "grid";

    /// <summary>The narrowest a column may get before the grid drops one; used when <see cref="Columns" /> is 0.</summary>
    [NamedSetting("min-column")]
    public float MinColumn
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = 152;

    /// <summary>A fixed number of columns; 0 (the default) fits as many as <see cref="MinColumn" /> allows.</summary>
    [NamedSetting("columns")]
    public int Columns
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    protected override (float Width, float Height) Arrange(float width, float height, bool commit)
    {
        var flow = new List<UIElement>();
        var placed = new List<UIElement>();
        var regions = new List<Region>();
        Collect(Children, flow, placed, regions);

        var inner = Math.Max(0, width - InsetLeft - InsetRight);
        if (commit)
            foreach (var region in regions)
                PlaceChild(region, 0, 0, Math.Max(0, width), float.IsInfinity(height) ? 0 : height);

        var columns = Columns > 0 ? Columns : Math.Max(1, (int)((inner + ColumnGap) / (MinColumn + ColumnGap)));
        var cell = Math.Max(0, (inner - ColumnGap * (columns - 1)) / columns);
        float y = 0;
        for (var first = 0; first < flow.Count; first += columns)
        {
            var count = Math.Min(columns, flow.Count - first);
            float row = 0;
            for (var index = 0; index < count; index++) row = Math.Max(row, flow[first + index].Measure(cell, 100000).height);

            if (commit)
                for (var index = 0; index < count; index++)
                    PlaceChild(flow[first + index], InsetLeft + index * (cell + ColumnGap), InsetTop + y - ScrollY, cell, row);

            y += row;
            if (first + columns < flow.Count) y += RowGap;
        }

        if (commit)
            foreach (var child in placed)
                PlaceAbsolute(child, Math.Max(0, width), float.IsInfinity(height) ? y + InsetTop + InsetBottom : height);

        return (inner, y);
    }
}
