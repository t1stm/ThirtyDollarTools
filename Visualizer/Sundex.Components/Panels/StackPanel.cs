using Sundex.Components.Abstractions;
using Sundex.Engine.Renderer.Abstract.Extensions;

namespace Sundex.Components.Panels;

public class StackPanel(UIContext context)
    : Panel(context), IPositioningElement
{
    public override string Tag => "stack";

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
        var start_x = Computed.AbsoluteX + Padding;
        var start_y = Computed.AbsoluteY + Padding;
        var inner_width = Computed.Width - 2 * Padding;
        var inner_height = Computed.Height - 2 * Padding;

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
                child.X = start_x - Computed.AbsoluteX;
                child.Y = offset - Computed.AbsoluteY;
                
                var ch = child.Height.IsPercentage ? inner_height * (child.Height.Value / 100f) : child.Height.Value;
                offset += ch + Spacing;
            }
            else
            {
                child.X = offset - Computed.AbsoluteX;
                child.Y = start_y - Computed.AbsoluteY;
                
                var cw = child.Width.IsPercentage ? inner_width * (child.Width.Value / 100f) : child.Width.Value;
                offset += cw + Spacing;
            }

            child.Layout();
        }

        Background?.SetPosition((start_x, start_y, 0));
        Background?.Scale = (Computed.Width, Computed.Height, 1);
    }
}