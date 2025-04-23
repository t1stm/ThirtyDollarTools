using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.UI;

public class FlexPanel(float x, float y, float width, float height)
    : UIElement(x, y, width, height), IPositioningElement, IColoredBackground
{
    private Align vertical = Align.Start;
    private Align horizontal = Align.Start;

    public LayoutDirection Direction { get; set; } = LayoutDirection.Horizontal;
    public float Padding { get; set; }
    public float Spacing { get; set; }
    public ColoredPlane? Background { get; set; }
    
    public bool AutoSizeSelf { get; set; }

    public Align HorizontalAlign
    {
        get => horizontal;
        set
        {
            horizontal = value;
            Layout();
        }
    }

    public Align VerticalAlign
    {
        get => vertical;
        set
        {
            vertical = value;
            Layout();
        }
    }

    public override void Layout()
    {
        var count = Children.Count;

        if (AutoSizeSelf)
        {
            AutoSize(count);
        }

        var inner_width = Width - 2 * Padding;
        var inner_height = Height - 2 * Padding;

        if (count < 1)
        {
            Background?.SetPosition((X, Y, 0));
            Background?.SetScale((Width, Height, 1));
            return;
        }

        if (Direction == LayoutDirection.Horizontal)
            Layout_Horizontal(count, inner_width, inner_height);
        else
            Layout_Vertical(count, inner_height, inner_width);

        Background?.SetPosition((X, Y, 0));
        Background?.SetScale((Width, Height, 1));
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
        {
            if (Direction == LayoutDirection.Vertical)
                Height = 2 * Padding + (count > 0 ? Children.Sum(c => c.Height) + Spacing * (count - 1) : 0);
            else
                Height = 2 * Padding + (count > 0 ? Children.Max(c => c.Height) : 0);
        }
    }

    private void Layout_Horizontal(int count, float inner_width, float inner_height)
    {
        var flex_count = Children.Count(c => c.AutoWidth);
        var total_fixed = Children.Where(c => !c.AutoWidth).Sum(c => c.Width);
        var total_spacing = Spacing * (count - 1);
        var free_space = inner_width - total_fixed - total_spacing;
        var flex_size = flex_count > 0 ? free_space / flex_count : 0;

        foreach (var child in Children.Where(child => child.AutoWidth))
            child.Width = flex_size;

        var offset = HorizontalAlign switch
        {
            Align.Center => (inner_width - total_fixed - total_spacing) / 2,
            Align.End => inner_width - total_fixed - total_spacing,
            _ => 0
        };

        foreach (var child in Children)
        {
            child.X = Padding + offset;

            if (child.AutoHeight)
                child.Height = inner_height;

            switch (VerticalAlign)
            {
                case Align.Center:
                    child.Y = Padding + (inner_height - child.Height) / 2;
                    break;
                case Align.End:
                    child.Y = Padding + inner_height - child.Height;
                    break;
                case Align.Stretch:
                    child.Y = Padding;
                    child.Height = inner_height;
                    break;
                default:
                    child.Y = Padding;
                    break;
            }

            child.Layout();
            offset += child.Width + Spacing;
        }
    }

    private void Layout_Vertical(int count, float inner_height, float inner_width)
    {
        var flex_count = Children.Count(c => c.AutoHeight);
        var total_fixed = Children.Where(c => !c.AutoHeight).Sum(c => c.Height);
        var total_spacing = Spacing * (count - 1);
        var free_space = inner_height - total_fixed - total_spacing;
        var flex_size = flex_count > 0 ? free_space / flex_count : 0;

        foreach (var child in Children.Where(child => child.AutoHeight))
            child.Height = flex_size;

        var offset = VerticalAlign switch
        {
            Align.Center => (inner_height - total_fixed - total_spacing) / 2,
            Align.End => inner_height - total_fixed - total_spacing,
            _ => 0
        };

        foreach (var child in Children)
        {
            child.Y = Padding + offset;

            if (child.AutoWidth)
                child.Width = inner_width;

            switch (HorizontalAlign)
            {
                case Align.Center:
                    child.X = Padding + (inner_width - child.Width) / 2;
                    break;
                case Align.End:
                    child.X = Padding + inner_width - child.Width;
                    break;
                case Align.Stretch:
                    child.X = Padding;
                    child.Width = inner_width;
                    break;
                default:
                    child.X = Padding;
                    break;
            }

            child.Layout();
            offset += child.Height + Spacing;
        }
    }

    protected override void DrawSelf(Camera camera)
    {
        Background?.Render(camera);
    }
}