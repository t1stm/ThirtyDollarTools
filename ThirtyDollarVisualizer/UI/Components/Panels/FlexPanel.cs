using ThirtyDollarVisualizer.UI.Abstractions;

namespace ThirtyDollarVisualizer.UI.Components.Panels;

public class FlexPanel(float x = 0, float y = 0, float width = 0, float height = 0)
    : Panel(x, y, width, height), IPositioningElement
{
    private Align _horizontal = Align.Start;
    private Align _vertical = Align.Start;
    public bool AutoSizeSelf { get; set; }

    public Align HorizontalAlign
    {
        get => _horizontal;
        set
        {
            _horizontal = value;
            Layout();
        }
    }

    public Align VerticalAlign
    {
        get => _vertical;
        set
        {
            _vertical = value;
            Layout();
        }
    }

    public LayoutDirection Direction { get; set; } = LayoutDirection.Horizontal;
    public float Padding { get; set; }
    public float Spacing { get; set; }

    public override void Layout()
    {
        var count = Children.Count;
        var a_x = AbsoluteX;
        var a_y = AbsoluteY;

        if (AutoSizeSelf) AutoSize(count);

        var inner_width = Width - 2 * Padding;
        var inner_height = Height - 2 * Padding;

        if (count < 1)
        {
            Background?.SetPosition((a_x, a_y, 0));
            if (Background != null)
                Background.Scale = (Width, Height, 1);
            return;
        }

        if (Direction == LayoutDirection.Horizontal)
            Layout_Horizontal(count, inner_width, inner_height);
        else
            Layout_Vertical(count, inner_height, inner_width);

        Background?.SetPosition((a_x, a_y, 0));
        if (Background != null)
            Background.Scale = (Width, Height, 1);
    }

    protected void AutoSize(int count)
    {
        if (AutoWidth)
        {
            if (Direction == LayoutDirection.Horizontal)
                Width = 2 * Padding + (count > 0 ? Children.Sum(c => c.Width) + Spacing * (count - 1) : 0);
            else
                Width = 2 * Padding + (count > 0 ? Children.Max(c => c.Width) : 0);
        }

        if (!AutoHeight) return;

        if (Direction == LayoutDirection.Vertical)
            Height = 2 * Padding + (count > 0 ? Children.Sum(c => c.Height) + Spacing * (count - 1) : 0);
        else
            Height = 2 * Padding + (count > 0 ? Children.Max(c => c.Height) : 0);
    }

    private void Layout_Horizontal(int count, float innerWidth, float innerHeight)
    {
        var flex_count = Children.Count(c => c.AutoWidth);
        var total_fixed = Children.Where(c => !c.AutoWidth).Sum(c => c.Width);
        var total_spacing = Spacing * (count - 1);
        var free_space = innerWidth - total_fixed - total_spacing;
        var flex_size = flex_count > 0 ? free_space / flex_count : 0;

        foreach (var child in Children.Where(child => child.AutoWidth && child is not FlexPanel { AutoSizeSelf: true }))
            child.Width = flex_size;

        // Recalculate total width after setting AutoWidth elements
        var total_width = Children.Sum(c => c.Width);

        var offset = HorizontalAlign switch
        {
            Align.Center => (innerWidth - total_width - total_spacing) / 2,
            Align.End => innerWidth - total_width - total_spacing,
            _ => 0
        };

        foreach (var child in Children)
        {
            child.X = Padding + offset;

            if (child.AutoHeight)
                child.Height = innerHeight;

            switch (VerticalAlign)
            {
                case Align.Center:
                    child.Y = Padding + (innerHeight - child.Height) / 2;
                    break;
                case Align.End:
                    child.Y = Padding + innerHeight - child.Height;
                    break;
                case Align.Stretch:
                    child.Y = Padding;
                    child.Height = innerHeight;
                    break;
                default:
                    child.Y = Padding;
                    break;
            }

            child.Layout();
            offset += child.Width + Spacing;
        }
    }

    private void Layout_Vertical(int count, float innerHeight, float innerWidth)
    {
        var flex_count = Children.Count(c => c.AutoHeight);
        var total_fixed = Children.Where(c => !c.AutoHeight).Sum(c => c.Height);
        var total_spacing = Spacing * (count - 1);
        var free_space = innerHeight - total_fixed - total_spacing;
        var flex_size = flex_count > 0 ? free_space / flex_count : 0;

        foreach (var child in Children.Where(child =>
                     child.AutoHeight && child is not FlexPanel { AutoSizeSelf: true }))
            child.Height = flex_size;

        // Recalculate total height after setting AutoHeight elements
        var total_height = Children.Sum(c => c.Height);

        var offset = VerticalAlign switch
        {
            Align.Center => (innerHeight - total_height - total_spacing) / 2,
            Align.End => innerHeight - total_height - total_spacing,
            _ => 0
        };

        foreach (var child in Children)
        {
            child.Y = Padding + offset;

            if (child.AutoWidth)
                child.Width = innerWidth;

            switch (HorizontalAlign)
            {
                case Align.Center:
                    child.X = Padding + (innerWidth - child.Width) / 2;
                    break;
                case Align.End:
                    child.X = Padding + innerWidth - child.Width;
                    break;
                case Align.Stretch:
                    child.X = Padding;
                    child.Width = innerWidth;
                    break;
                default:
                    child.X = Padding;
                    break;
            }

            child.Layout();
            offset += child.Height + Spacing;
        }
    }
}