using System.Reflection;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Components.Panels;

public class FlexPanel(UIContext context) : Panel(context), IPositioningElement
{
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

    [NamedSetting("width")]
    public override LiteralOrComputable Width { get; set; } = LiteralOrComputable.AutoSize;
    
    [NamedSetting("height")]
    public override LiteralOrComputable Height { get; set; } = LiteralOrComputable.AutoSize;

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

    public override (float width, float height) Measure(float parentWidth, float parentHeight)
    {
        // If explicit sizes are provided (not Auto), default measurement is fine
        var explicitW = !Width.Auto ? Width.Resolve(parentWidth) : (float?)null;
        var explicitH = !Height.Auto ? Height.Resolve(parentHeight) : (float?)null;

        // Available inner space to measure children against
        var baseW = explicitW ?? parentWidth;
        var baseH = explicitH ?? parentHeight;

        var innerW = Math.Max(0, baseW - 2 * Padding);
        var innerH = Math.Max(0, baseH - 2 * Padding);

        float contentW = 0;
        float contentH = 0;

        if (Children.Count == 0)
        {
            contentW = 0;
            contentH = 0;
        }
        else if (Direction == LayoutDirection.Horizontal)
        {
            float sumW = 0;
            float maxH = 0;
            var i = 0;
            foreach (var child in Children)
            {
                var (cw, ch) = child.Measure(innerW, innerH);
                sumW += cw;
                if (i++ > 0) sumW += Spacing;
                if (ch > maxH) maxH = ch;
            }
            contentW = sumW;
            contentH = maxH;
        }
        else // Vertical
        {
            float sumH = 0;
            float maxW = 0;
            var i = 0;
            foreach (var child in Children)
            {
                var (cw, ch) = child.Measure(innerW, innerH);
                sumH += ch;
                if (i++ > 0) sumH += Spacing;
                if (cw > maxW) maxW = cw;
            }
            contentW = maxW;
            contentH = sumH;
        }

        var measuredW = (explicitW ?? (contentW + 2 * Padding));
        var measuredH = (explicitH ?? (contentH + 2 * Padding));

        return (measuredW, measuredH);
    }

    private void Layout_Horizontal(int count, float innerWidth, float innerHeight)
    {
        var total_spacing = Spacing * (count - 1);
        var total_fixed = Children.Where(c => !c.Width.IsPercentage && !c.Width.Auto).Sum(c => c.Width.Value);
        var total_auto = Children.Where(c => c.Width.Auto).Sum(c => c.Measure(innerWidth, innerHeight).width);
        var total_percent = Children.Where(c => c.Width.IsPercentage).Sum(c => c.Width.Value);
        var free_space = Math.Max(0, innerWidth - total_fixed - total_auto - total_spacing);
        var total_width = total_fixed + total_auto + (total_percent > 0 ? free_space : 0);

        var offset = HorizontalAlign switch
        {
            Align.Center => (innerWidth - total_width - total_spacing) / 2,
            Align.End => innerWidth - total_width - total_spacing,
            _ => 0
        };

        foreach (var child in Children)
        {
            var (measuredW, measuredH) = child.Measure(innerWidth, innerHeight);

            child.X = offset;

            // Resolve child height
            var ch = child.Height.Auto ? measuredH
                : child.Height.IsPercentage ? innerHeight * (child.Height.Value / 100f)
                : child.Height.Value;
            if (child.Height.IsPercentage || child.Height.Auto)
                child.Height = ch;
            switch (VerticalAlign)
            {
                case Align.Center:
                    child.Y = (innerHeight - ch) / 2;
                    break;
                case Align.End:
                    child.Y = innerHeight - ch;
                    break;
                case Align.Stretch:
                    child.Y = 0;
                    child.Height = innerHeight;
                    break;
                case Align.Start:
                default:
                    child.Y = 0;
                    break;
            }

            // Resolve child width
            float cw;
            if (child.Width.IsPercentage)
                cw = total_percent > 0 ? free_space * (child.Width.Value / total_percent) : 0;
            else if (child.Width.Auto)
                cw = measuredW;
            else
                cw = child.Width.Value;

            if (child.Width.IsPercentage || child.Width.Auto)
                child.Width = cw;

            child.Layout();
            offset += cw + Spacing;
        }
    }

    private void Layout_Vertical(int count, float innerHeight, float innerWidth)
    {
        var total_spacing = Spacing * (count - 1);
        var total_fixed = Children.Where(c => !c.Height.IsPercentage && !c.Height.Auto).Sum(c => c.Height.Value);
        var total_auto = Children.Where(c => c.Height.Auto).Sum(c => c.Measure(innerWidth, innerHeight).height);
        var total_percent = Children.Where(c => c.Height.IsPercentage).Sum(c => c.Height.Value);
        var free_space = Math.Max(0, innerHeight - total_fixed - total_auto - total_spacing);
        var total_height = total_fixed + total_auto + (total_percent > 0 ? free_space : 0);

        var offset = VerticalAlign switch
        {
            Align.Center => (innerHeight - total_height - total_spacing) / 2,
            Align.End => innerHeight - total_height - total_spacing,
            _ => 0
        };

        foreach (var child in Children)
        {
            var (measuredW, measuredH) = child.Measure(innerWidth, innerHeight);

            child.Y = offset;

            // Resolve child width
            var cw = child.Width.Auto ? measuredW
                : child.Width.IsPercentage ? innerWidth * (child.Width.Value / 100f)
                : child.Width.Value;
            if (child.Width.IsPercentage || child.Width.Auto)
                child.Width = cw;
            switch (HorizontalAlign)
            {
                case Align.Center:
                    child.X = (innerWidth - cw) / 2;
                    break;
                case Align.End:
                    child.X = innerWidth - cw;
                    break;
                case Align.Stretch:
                    child.X = 0;
                    child.Width = innerWidth;
                    break;
                case Align.Start:
                default:
                    child.X = 0;
                    break;
            }

            // Resolve child height
            float ch;
            if (child.Height.IsPercentage)
                ch = total_percent > 0 ? free_space * (child.Height.Value / total_percent) : 0;
            else if (child.Height.Auto)
                ch = measuredH;
            else
                ch = child.Height.Value;

            if (child.Height.IsPercentage || child.Height.Auto)
                child.Height = ch;

            child.Layout();
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