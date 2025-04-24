using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.UI;

public class StackPanel(float x, float y, float width, float height)
    : UIElement(x, y, width, height), IPositioningElement, IColoredBackground
{
    public LayoutDirection Direction { get; set; } = LayoutDirection.Vertical;
    public float Spacing { get; set; } = 0;
    public float Padding { get; set; } = 0;
    public ColoredPlane? Background { get; set; }

    public StackPanel() : this(0, 0, 0, 0)
    {
    }

    public override void Layout()
    {
        var start_x = AbsoluteX + Padding;
        var start_y = AbsoluteY + Padding;

        var offset = Direction switch
        {
            LayoutDirection.Horizontal => start_x,
            LayoutDirection.Vertical => start_y,
            _ => throw new ArgumentOutOfRangeException()
        };

        foreach (var child in Children)
        {
            if (Direction == LayoutDirection.Vertical)
            {
                child.X = start_x;
                child.Y = offset;

                if (child.AutoWidth) child.Width = Width - 2 * Padding;
                offset += child.Height + Spacing;
            }
            else
            {
                child.X = offset;
                child.Y = start_y;

                if (child.AutoHeight) child.Height = Height - 2 * Padding;
                offset += child.Width + Spacing;
            }

            child.Layout();
        }

        Background?.SetPosition((start_x, start_y, 0));
        Background?.SetScale((Width, Height, 1));
    }

    protected override void DrawSelf(UIContext context)
    {
        if (Background != null)
            context.QueueRender(Background, Index);
    }
}