using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;

namespace Sundex.Components.Panels;

/// <summary>
///     A cell of a <see cref="Table" />: a flex box. <see cref="Span" /> takes a whole row (HTML's
///     <c>colspan</c> across every column); <see cref="Wraps" /> marks a column of running text,
///     the first to give way when the table is too wide.
/// </summary>
[MarkupTag("td")]
public class TableCell(UIContext context) : FlexPanel(context)
{
    public override string Tag => "td";

    [NamedSetting("span")]
    public bool Span { get; set; }

    [NamedSetting("wraps")]
    public bool Wraps { get; set; }
}

/// <summary>
///     An auto-layout <c>&lt;table&gt;</c>: its flow children are cells, read row by row,
///     <see cref="Columns" /> to a row. A column is as wide as its widest cell, spare room is shared
///     out by width, and only when the narrowest wrapping columns can't fit does the table scroll
///     (with <c>overflow-x = "scroll"</c>). A row is as tall as its tallest cell, and every cell in
///     it is stretched to that. Regions are transparent, so an <c>&lt;each&gt;</c> can own one row
///     of cells at a time.
/// </summary>
[MarkupTag("table")]
public class Table(UIContext context) : FlexPanel(context)
{
    private const float WrapMin = 144;
    private const float WrapMax = 416;

    public override string Tag => "table";

    [NamedSetting("columns")]
    public int Columns
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = 1;

    protected override (float Width, float Height) Arrange(float width, float height, bool commit)
    {
        var flow = new List<UIElement>();
        var placed = new List<UIElement>();
        var regions = new List<Region>();
        Collect(Children, flow, placed, regions);
        var columns = Math.Max(1, Columns);

        var inner = Math.Max(0, width - InsetLeft - InsetRight);
        if (commit)
            foreach (var region in regions)
                PlaceChild(region, 0, 0, Math.Max(0, width), float.IsInfinity(height) ? 0 : height);

        // First pass: the natural width of every column, and the least it can give way to.
        Span<float> natural = stackalloc float[columns];
        Span<float> least = stackalloc float[columns];
        var column = 0;
        foreach (var element in flow)
        {
            if (element is TableCell { Span: true })
            {
                column = 0;
                continue;
            }

            var (cellWidth, _) = element.Measure(100000, 100000);
            var wraps = element is TableCell { Wraps: true };
            var wanted = wraps ? Math.Clamp(cellWidth, WrapMin, WrapMax) : cellWidth;
            natural[column] = Math.Max(natural[column], wanted);
            least[column] = Math.Max(least[column], wraps ? WrapMin : cellWidth);
            column = (column + 1) % columns;
        }

        float sum = 0, give = 0;
        for (var columnIndex = 0; columnIndex < columns; columnIndex++)
        {
            sum += natural[columnIndex];
            give += natural[columnIndex] - least[columnIndex];
        }

        Span<float> widths = stackalloc float[columns];
        for (var columnIndex = 0; columnIndex < columns; columnIndex++)
            if (sum <= inner)
                widths[columnIndex] = sum > 0 ? natural[columnIndex] * inner / sum : inner / columns;
            else if (give > 0)
                widths[columnIndex] = natural[columnIndex] - (natural[columnIndex] - least[columnIndex]) * Math.Min(1, (sum - inner) / give);
            else
                widths[columnIndex] = natural[columnIndex];

        float total = 0;
        for (var columnIndex = 0; columnIndex < columns; columnIndex++) total += widths[columnIndex];

        // Second pass: the rows, at those widths.
        float y = 0;
        var at = 0;
        while (at < flow.Count)
        {
            if (flow[at] is TableCell { Span: true } span)
            {
                var full = Math.Max(total, inner);
                var spanHeight = span.Measure(full, 100000).height;
                if (commit) PlaceChild(span, InsetLeft - ScrollX, InsetTop + y - ScrollY, full, spanHeight);
                y += spanHeight;
                at++;
                continue;
            }

            float row = 0;
            var count = Math.Min(columns, flow.Count - at);
            for (var index = 0; index < count && flow[at + index] is not TableCell { Span: true }; index++)
                row = Math.Max(row, flow[at + index].Measure(widths[index], 100000).height);

            float x = 0;
            var taken = 0;
            for (var index = 0; index < count && flow[at + index] is not TableCell { Span: true }; index++)
            {
                if (commit) PlaceChild(flow[at + index], InsetLeft + x - ScrollX, InsetTop + y - ScrollY, widths[index], row);
                x += widths[index];
                taken++;
            }

            at += Math.Max(1, taken);
            y += row;
        }

        if (commit)
            foreach (var child in placed)
                PlaceAbsolute(child, Math.Max(0, width), float.IsInfinity(height) ? y + InsetTop + InsetBottom : height);

        return (total, y);
    }
}
