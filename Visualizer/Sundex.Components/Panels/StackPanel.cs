using Sundex.Components.Abstractions;
using Sundex.Engine.Renderer.Abstract.Extensions;

namespace Sundex.Components.Panels;

public class StackPanel(UIContext context, float x = 0, float y = 0, float width = 0, float height = 0)
    : Panel(context, x, y, width, height), IPositioningElement
{
    public LayoutDirection Direction
    {
        get;
        set
        {
            field = value;
            InvalidateLayout();
        }
    } = LayoutDirection.Vertical;

    public float Spacing
    {
        get;
        set
        {
            field = value;
            InvalidateLayout();
        }
    } = 0;

    public float Padding
    {
        get;
        set
        {
            field = value;
            InvalidateLayout();
        }
    } = 0;

    protected override void DoLayout()
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
                child.X = start_x - AbsoluteX;
                child.Y = offset - AbsoluteY;

                if (child.AutoWidth) child.Width = Width - 2 * Padding;
                offset += child.Height + Spacing;
            }
            else
            {
                child.X = offset - AbsoluteX;
                child.Y = start_y - AbsoluteY;

                if (child.AutoHeight) child.Height = Height - 2 * Padding;
                offset += child.Width + Spacing;
            }

            child.Layout();
        }

        Background?.SetPosition((start_x, start_y, 0));
        Background?.Scale = (Width, Height, 1);
    }
}