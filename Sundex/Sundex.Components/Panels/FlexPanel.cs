using System.Reflection;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Components.Panels;

public class FlexPanel(UIContext context) : Panel(context)
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

    [NamedSetting("width")] public override LiteralOrComputable Width { get; set; } = LiteralOrComputable.AutoSize;

    [NamedSetting("height")] public override LiteralOrComputable Height { get; set; } = LiteralOrComputable.AutoSize;

    [NamedSetting("wrap")]
    public virtual bool Wrap
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateLayout();
        }
    } = false;

    protected override void DoLayout()
    {
        // Parent-assigned sizes are pass-scoped: the placement below recomputes them,
        // and stale ones must not survive a Direction/Wrap change.
        foreach (var child in Children)
        {
            child.ParentAssignedWidth = null;
            child.ParentAssignedHeight = null;
        }

        var count = Children.Count;

        var inner_width = Computed.Width - 2 * Padding;
        var inner_height = Computed.Height - 2 * Padding;

        // Placement runs before base.DoLayout()'s own child.Layout() pass: that pass would
        // otherwise be the first layout a percent-sized child sees, with ParentAssignedHeight
        // still null — a stateful child (e.g. ScrollView clamping its scroll offset against
        // its own Computed.Height) would corrupt itself against that wrong, too-large size.
        if (count >= 1)
        {
            if (Direction == LayoutDirection.Horizontal)
                Layout_Horizontal(count, inner_width, inner_height);
            else
                Layout_Vertical(count, inner_height, inner_width);
        }

        base.DoLayout();
    }

    public override (float width, float height) Measure(float parentWidth, float parentHeight)
    {
        var explicitW = !Width.Auto ? Width.Resolve(parentWidth) : (float?)null;
        var explicitH = !Height.Auto ? Height.Resolve(parentHeight) : (float?)null;

        var baseW = explicitW ?? parentWidth;
        var baseH = explicitH ?? parentHeight;

        var innerW = Math.Max(0, baseW - 2 * Padding);
        var innerH = Math.Max(0, baseH - 2 * Padding);

        float contentW;
        float contentH;

        if (Children.Count == 0) return (explicitW ?? 0, explicitH ?? 0);

        switch (Direction)
        {
            case LayoutDirection.Horizontal:
                {
                    if (Wrap)
                    {
                        float currentLineWidth = 0;
                        float currentLineHeight = 0;
                        float totalWidth = 0;
                        float totalHeight = 0;
                        var firstInLine = true;

                        foreach (var child in Children)
                        {
                            var (cw, ch) = child.Measure(innerW, innerH);
                            var potentialWidth = firstInLine ? cw : currentLineWidth + Spacing + cw;

                            if (!firstInLine && potentialWidth > innerW && innerW > 0)
                            {
                                // Wrap to next line
                                totalWidth = Math.Max(totalWidth, currentLineWidth);
                                totalHeight += currentLineHeight + Spacing;
                                currentLineWidth = cw;
                                currentLineHeight = ch;
                                firstInLine = false;
                            }
                            else
                            {
                                currentLineWidth = potentialWidth;
                                currentLineHeight = Math.Max(currentLineHeight, ch);
                                firstInLine = false;
                            }
                        }

                        totalWidth = Math.Max(totalWidth, currentLineWidth);
                        totalHeight += currentLineHeight;

                        contentW = totalWidth;
                        contentH = totalHeight;
                    }
                    else
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

                    break;
                }
            case LayoutDirection.Vertical:
                {
                    if (Wrap)
                    {
                        float currentColumnHeight = 0;
                        float currentColumnWidth = 0;
                        float totalHeight = 0;
                        float totalWidth = 0;
                        var firstInColumn = true;

                        foreach (var child in Children)
                        {
                            var (cw, ch) = child.Measure(innerW, innerH);
                            var potentialHeight = firstInColumn ? ch : currentColumnHeight + Spacing + ch;

                            if (!firstInColumn && potentialHeight > innerH && innerH > 0)
                            {
                                // Wrap to next column
                                totalHeight = Math.Max(totalHeight, currentColumnHeight);
                                totalWidth += currentColumnWidth + Spacing;
                                currentColumnHeight = ch;
                                currentColumnWidth = cw;
                                firstInColumn = false;
                            }
                            else
                            {
                                currentColumnHeight = potentialHeight;
                                currentColumnWidth = Math.Max(currentColumnWidth, cw);
                                firstInColumn = false;
                            }
                        }

                        totalHeight = Math.Max(totalHeight, currentColumnHeight);
                        totalWidth += currentColumnWidth;

                        contentW = totalWidth;
                        contentH = totalHeight;
                    }
                    else
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

                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(Direction), "Invalid layout direction value.");
        }

        var measuredW = explicitW ?? contentW + 2 * Padding;
        var measuredH = explicitH ?? contentH + 2 * Padding;

        return (measuredW, measuredH);
    }

    private void Layout_Horizontal(int count, float innerWidth, float innerHeight)
    {
        if (Wrap)
        {
            Layout_Horizontal_Wrap(innerWidth, innerHeight);
            return;
        }

        var total_spacing = Spacing * (count - 1);
        float total_fixed = 0, total_auto = 0, total_percent = 0;
        foreach (var c in Children)
            if (c.Width.Auto) total_auto += c.Measure(innerWidth, innerHeight).width;
            else if (c.Width.IsPercentage) total_percent += c.Width.Value;
            else total_fixed += c.Width.Value;
        var free_space = Math.Max(0, innerWidth - total_fixed - total_auto - total_spacing);
        var total_width = total_fixed + total_auto + free_space * total_percent / 100f;

        var offset = HorizontalAlign switch
        {
            Align.Center => (innerWidth - total_width - total_spacing) / 2,
            Align.End => innerWidth - total_width - total_spacing,
            _ => 0
        };

        foreach (var child in Children)
        {
            if (child.Width.IsPercentage) child.ParentAssignedWidth = child.Width.Value / 100f * free_space;

            child.X = offset;
            child.Layout();

            switch (VerticalAlign)
            {
                case Align.Center:
                    child.Y = (innerHeight - child.Computed.Height) / 2;
                    break;
                case Align.End:
                    child.Y = innerHeight - child.Computed.Height;
                    break;
                case Align.Stretch:
                    child.Y = 0;
                    // child.Height = innerHeight; // Still destructive, but matches Stretch behavior
                    break;
                case Align.Start:
                default:
                    child.Y = 0;
                    break;
            }

            child.Layout(); // Re-layout with new relative Y if changed
            offset += child.Computed.Width + Spacing;
        }
    }

    private void Layout_Vertical(int count, float innerHeight, float innerWidth)
    {
        if (Wrap)
        {
            Layout_Vertical_Wrap(innerHeight, innerWidth);
            return;
        }

        var total_spacing = Spacing * (count - 1);
        float total_fixed = 0, total_auto = 0, total_percent = 0;
        foreach (var c in Children)
            if (c.Height.Auto) total_auto += c.Measure(innerWidth, innerHeight).height;
            else if (c.Height.IsPercentage) total_percent += c.Height.Value;
            else total_fixed += c.Height.Value;
        var free_space = Math.Max(0, innerHeight - total_fixed - total_auto - total_spacing);
        var total_height = total_fixed + total_auto + free_space * total_percent / 100f;

        var offset = VerticalAlign switch
        {
            Align.Center => (innerHeight - total_height - total_spacing) / 2,
            Align.End => innerHeight - total_height - total_spacing,
            _ => 0
        };

        foreach (var child in Children)
        {
            if (child.Height.IsPercentage) child.ParentAssignedHeight = child.Height.Value / 100f * free_space;

            child.Y = offset;
            child.Layout();

            switch (HorizontalAlign)
            {
                case Align.Center:
                    child.X = (innerWidth - child.Computed.Width) / 2;
                    break;
                case Align.End:
                    child.X = innerWidth - child.Computed.Width;
                    break;
                case Align.Stretch:
                    child.X = 0;
                    // child.Width = innerWidth; // Still destructive, but matches Stretch behavior
                    break;
                case Align.Start:
                default:
                    child.X = 0;
                    break;
            }

            child.Layout(); // Re-layout with new relative X if changed
            offset += child.Computed.Height + Spacing;
        }
    }

    private void Layout_Horizontal_Wrap(float innerWidth, float innerHeight)
    {
        float currentX = 0;
        float currentY = 0;
        float lineHeight = 0;
        var firstInLine = true;

        foreach (var child in Children)
        {
            var (cw, _) = child.Measure(innerWidth, innerHeight);

            // currentX already includes the trailing spacing from the previous item, so
            // it's the exact position this child would start at — no extra Spacing here.
            if (!firstInLine && currentX + cw > innerWidth && innerWidth > 0)
            {
                currentX = 0;
                currentY += lineHeight + Spacing;
                lineHeight = 0;
            }

            child.X = currentX;
            child.Y = currentY;
            child.Layout();

            currentX += child.Computed.Width + Spacing;
            lineHeight = Math.Max(lineHeight, child.Computed.Height);
            firstInLine = false;
        }
    }

    private void Layout_Vertical_Wrap(float innerHeight, float innerWidth)
    {
        float currentX = 0;
        float currentY = 0;
        float columnWidth = 0;
        var firstInColumn = true;

        foreach (var child in Children)
        {
            var (_, ch) = child.Measure(innerWidth, innerHeight);

            // currentY already includes the trailing spacing from the previous item, so
            // it's the exact position this child would start at — no extra Spacing here.
            if (!firstInColumn && currentY + ch > innerHeight && innerHeight > 0)
            {
                currentY = 0;
                currentX += columnWidth + Spacing;
                columnWidth = 0;
            }

            child.X = currentX;
            child.Y = currentY;
            child.Layout();

            currentY += child.Computed.Height + Spacing;
            columnWidth = Math.Max(columnWidth, child.Computed.Width);
            firstInColumn = false;
        }
    }

    protected override void ApplyStyleValue(StyleSheet styleSheet, IStyleValue? styleValue, PropertyInfo propertyInfo)
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
                    base.ApplyStyleValue(styleSheet, styleValue, propertyInfo);
                    return;
                }
        }
    }
}