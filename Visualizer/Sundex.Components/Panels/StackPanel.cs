using System.Reflection;
using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Components.Panels;

public class StackPanel(UIContext context)
    : Panel(context), IPositioningElement
{
    public override string Tag => "stack";
    
    [NamedSetting("direction")]
    public LayoutDirection Direction
    {
        get;
        set
        {
            field = value;
            InvalidateLayout();
        }
    } = LayoutDirection.Vertical;

    [NamedSetting("spacing")]
    public float Spacing
    {
        get;
        set
        {
            field = value;
            InvalidateLayout();
        }
    } = 0;

    [NamedSetting("padding")]
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
        base.DoLayout();
        
        var inner_width = Computed.Width - 2 * Padding;
        var inner_height = Computed.Height - 2 * Padding;

        float offset = 0;

        foreach (var child in Children)
        {
            if (Direction == LayoutDirection.Vertical)
            {
                child.X = 0;
                var currentY = offset;
                child.Y = currentY;
                
                var (cw, ch) = child.Measure(inner_width, inner_height);
                if (child.Width.IsPercentage)
                    child.Width = inner_width * (child.Width.Value / 100f);
                else if (child.Width.Auto)
                    child.Width = cw;

                if (child.Height.IsPercentage)
                    child.Height = inner_height * (child.Height.Value / 100f);
                else if (child.Height.Auto)
                    child.Height = ch;

                child.Layout();
                offset += child.Computed.Height + Spacing;
            }
            else
            {
                var currentX = offset;
                child.X = currentX;
                child.Y = 0;
                
                var (cw, ch) = child.Measure(inner_width, inner_height);
                if (child.Width.IsPercentage)
                    child.Width = inner_width * (child.Width.Value / 100f);
                else if (child.Width.Auto)
                    child.Width = cw;

                if (child.Height.IsPercentage)
                    child.Height = inner_height * (child.Height.Value / 100f);
                else if (child.Height.Auto)
                    child.Height = ch;

                child.Layout();
                offset += child.Computed.Width + Spacing;
            }
        }
    }

    public override (float width, float height) Measure(float parentWidth, float parentHeight)
    {
        var explicitW = !Width.Auto ? Width.Resolve(parentWidth) : (float?)null;
        var explicitH = !Height.Auto ? Height.Resolve(parentHeight) : (float?)null;

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
        else if (Direction == LayoutDirection.Vertical)
        {
            // Stack children top-to-bottom
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
        else // Horizontal
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

        var measuredW = (explicitW ?? (contentW + 2 * Padding));
        var measuredH = (explicitH ?? (contentH + 2 * Padding));

        return (measuredW, measuredH);
    }
    
    protected override void ApplyStyleValue(IStyleValue? styleValue, PropertyInfo propertyInfo)
    {
        if (styleValue is null) return;

        switch (styleValue)
        {
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