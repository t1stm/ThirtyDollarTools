namespace Sundex.Components.Abstractions.Values;

public class ComputedRectangle
{
    public ComputedRectangle(UIElement current)
    {
        UpdateAbsoluteBasedOnParent(current, current.Parent);
    }

    public float AbsoluteX { get; private set; }
    public float AbsoluteY { get; private set; }

    public float X { get; private set; }
    public float Y { get; private set; }
    public float Width { get; private set; }
    public float Height { get; private set; }

    public void UpdateAbsoluteBasedOnParent(UIElement current, UIElement? parent)
    {
        var parentWidth = parent?.Computed.Width ?? current.Context.ViewportWidth;
        var parentHeight = parent?.Computed.Height ?? current.Context.ViewportHeight;

        // Ask element for desired size (used when Auto is set)
        var (desiredW, desiredH) = current.Measure(parentWidth, parentHeight);

        Width = current.Width.Auto
            ? desiredW
            : current.Width.Resolve(parentWidth);

        Height = current.Height.Auto
            ? desiredH
            : current.Height.Resolve(parentHeight);

        // X and Y are relative to the parent's content origin (after padding).
        // The parent's layout pass is responsible for setting these.
        X = current.X.Resolve(parentWidth);
        Y = current.Y.Resolve(parentHeight);

        // Apply anchor offsets so that e.g. anchor-x="center" shifts the element left by half its width
        X += current.AnchorOffsetX(Width);
        Y += current.AnchorOffsetY(Height);

        // AbsoluteX/Y: start from parent's absolute origin, then add parent padding if it is a
        // positioning container (IPositioningElement), then add this element's own X/Y offset.
        var parentAbsX = parent?.Computed.AbsoluteX ?? 0;
        var parentAbsY = parent?.Computed.AbsoluteY ?? 0;
        var parentPadding = parent is IPositioningElement pe ? pe.Padding : 0;

        AbsoluteX = parentAbsX + parentPadding + X;
        AbsoluteY = parentAbsY + parentPadding + Y;
    }
}