using System.Reflection;
using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Components.Panels;

public class FlexPanel(UIContext context)
    : Panel(context), IPositioningElement
{
    [NamedSetting("autosize-self")] public bool AutoSizeSelf { get; set; }

    public override string Tag => "flex";

    [NamedSetting("horizontal-align")]
    public Align HorizontalAlign
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateLayout();
        }
    } = Align.Start;

    [NamedSetting("vertical-align")]
    public Align VerticalAlign
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateLayout();
        }
    } = Align.Start;

    [NamedSetting("direction")] public LayoutDirection Direction { get; set; } = LayoutDirection.Horizontal;

    [NamedSetting("padding")]
    public float Padding
    {
        get;
        set
        {
            field = value;
            InvalidateLayout();
        }
    }

    [NamedSetting("spacing")]
    public float Spacing
    {
        get;
        set
        {
            field = value;
            InvalidateLayout();
        }
    }

    protected override void DoLayout()
    {
        var count = Children.Count;
        var a_x = Computed.AbsoluteX;
        var a_y = Computed.AbsoluteY;

        var inner_width = Computed.Width - 2 * Padding;
        var inner_height = Computed.Height - 2 * Padding;

        if (count < 1)
        {
            Background?.SetPosition((a_x, a_y, 0));
            Background?.Scale = (Computed.Width, Computed.Height, 1);
            return;
        }

        if (Direction == LayoutDirection.Horizontal)
            Layout_Horizontal(count, inner_width, inner_height);
        else
            Layout_Vertical(count, inner_height, inner_width);

        Background?.SetPosition((a_x, a_y, 0));
        Background?.Scale = (Computed.Width, Computed.Height, 1);
    }

    private void Layout_Horizontal(int count, float innerWidth, float innerHeight)
    {
        var total_spacing = Spacing * (count - 1);
        var total_width = Children.Sum(c => c.Width.IsPercentage ? innerWidth * (c.Width.Value / 100f) : c.Width.Value);

        var offset = HorizontalAlign switch
        {
            Align.Center => (innerWidth - total_width - total_spacing) / 2,
            Align.End => innerWidth - total_width - total_spacing,
            _ => 0
        };

        foreach (var child in Children)
        {
            child.Layout();
            child.X = Padding + offset;

            var ch = child.Height.IsPercentage ? innerHeight * (child.Height.Value / 100f) : child.Height.Value;
            switch (VerticalAlign)
            {
                case Align.Center:
                    child.Y = Padding + (innerHeight - ch) / 2;
                    break;
                case Align.End:
                    child.Y = Padding + innerHeight - ch;
                    break;
                case Align.Stretch:
                    child.Y = Padding;
                    child.Height = innerHeight;
                    break;
                case Align.Start:
                default:
                    child.Y = Padding;
                    break;
            }

            child.Layout();
            var cw = child.Width.IsPercentage ? innerWidth * (child.Width.Value / 100f) : child.Width.Value;
            offset += cw + Spacing;
        }
    }

    private void Layout_Vertical(int count, float innerHeight, float innerWidth)
    {
        var total_spacing = Spacing * (count - 1);
        var total_height =
            Children.Sum(c => c.Height.IsPercentage ? innerHeight * (c.Height.Value / 100f) : c.Height.Value);

        var offset = VerticalAlign switch
        {
            Align.Center => (innerHeight - total_height - total_spacing) / 2,
            Align.End => innerHeight - total_height - total_spacing,
            _ => 0
        };

        foreach (var child in Children)
        {
            child.Layout();
            child.Y = Padding + offset;

            var cw = child.Width.IsPercentage ? innerWidth * (child.Width.Value / 100f) : child.Width.Value;
            switch (HorizontalAlign)
            {
                case Align.Center:
                    child.X = Padding + (innerWidth - cw) / 2;
                    break;
                case Align.End:
                    child.X = Padding + innerWidth - cw;
                    break;
                case Align.Stretch:
                    child.X = Padding;
                    child.Width = innerWidth;
                    break;
                case Align.Start:
                default:
                    child.X = Padding;
                    break;
            }

            child.Layout();
            var ch = child.Height.IsPercentage ? innerHeight * (child.Height.Value / 100f) : child.Height.Value;
            offset += ch + Spacing;
        }
    }

    protected override void ApplyStyleValue(IStyleValue? styleValue, PropertyInfo propertyInfo)
    {
        if (styleValue is null) return;

        switch (styleValue)
        {
            case StringValue sv when propertyInfo.PropertyType == typeof(Align):
            {
                Align? align = sv.Value switch
                { 
                    "center" => Align.Center,
                    "end" => Align.End,
                    "stretch" => Align.Stretch,
                    "start" => Align.Start,
                    _ => null
                };
                
                if (align is not null)
                    propertyInfo.SetValue(this, align.Value);
                return;
            }

            case StringValue sv when propertyInfo.PropertyType == typeof(LayoutDirection):
            {
                LayoutDirection? direction = sv.Value switch
                {
                    "horizontal" => LayoutDirection.Horizontal,
                    "vertical" => LayoutDirection.Vertical,
                    _ => null
                };
                
                if (direction is not null)
                    propertyInfo.SetValue(this, direction.Value);
                return;
            }
            
            default:
            {
                base.ApplyStyleValue(styleValue, propertyInfo);
                return;
            }
        }
    }
}