using ThirtyDollarVisualizer.UI.Abstractions;

namespace ThirtyDollarVisualizer.UI.Components.Panels;

public class StackPanel(float x, float y, float width, float height)
    : Panel(x, y, width, height), IPositioningElement
{
    public StackPanel() : this(0, 0, 0, 0)
    {
    }

    public LayoutDirection Direction { get; set; } = LayoutDirection.Vertical;
    public float Spacing { get; set; } = 0;
    public float Padding { get; set; } = 0;

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
        if (Background != null)
            Background.Scale = (Width, Height, 1);
    }
}