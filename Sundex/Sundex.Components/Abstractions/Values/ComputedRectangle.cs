namespace Sundex.Components.Abstractions.Values;

public class ComputedRectangle
{
    // No measuring here: this runs from the UIElement constructor, where virtual
    // Width/Height overrides (e.g. WindowFrame → Container.Width) aren't initialized yet.
    // The element starts with NeedsLayout = true, so the first Layout() computes everything.


    public Action? OnUpdate { get; set; }

    public float AbsoluteX { get; private set; }
    public float AbsoluteY { get; private set; }

    public float X { get; private set; }
    public float Y { get; private set; }
    public float Width { get; private set; }
    public float Height { get; private set; }

    public void UpdateAbsoluteBasedOnParent(UIElement current, UIElement? parent)
    {
        var oldW = Width;
        var oldH = Height;
        var oldX = X;
        var oldY = Y;
        var oldAbsX = AbsoluteX;
        var oldAbsY = AbsoluteY;

        var parentWidth = parent?.Computed.Width ?? current.Context.ViewportWidth;
        var parentHeight = parent?.Computed.Height ?? current.Context.ViewportHeight;

        var parentPadding = parent is IPositioningElement pe ? pe.Padding : 0;
        var innerWidth = Math.Max(0, parentWidth - 2 * parentPadding);
        var innerHeight = Math.Max(0, parentHeight - 2 * parentPadding);

        // Ask element for desired size (used when Auto is set)
        // Pass inner dimensions so that percentages and Auto resolve relative to the padded area.
        var (desiredW, desiredH) = current.Measure(innerWidth, innerHeight);

        Width = current.Width.Auto
            ? desiredW
            : current.Width.Resolve(innerWidth);

        Height = current.Height.Auto
            ? desiredH
            : current.Height.Resolve(innerHeight);

        // X and Y are relative to the parent's content origin (after padding).
        // The parent's layout pass is responsible for setting these.
        X = current.X.Resolve(innerWidth);
        Y = current.Y.Resolve(innerHeight);

        // Apply anchor offsets so that e.g. anchor-x="center" shifts the element left by half its width
        X += current.AnchorOffsetX(Width);
        Y += current.AnchorOffsetY(Height);

        // AbsoluteX/Y: start from parent's absolute origin, then add parent padding if it is a
        // positioning container (IPositioningElement), then add this element's own X/Y offset.
        var parentAbsX = parent?.Computed.AbsoluteX ?? 0;
        var parentAbsY = parent?.Computed.AbsoluteY ?? 0;

        AbsoluteX = parentAbsX + parentPadding + X;
        AbsoluteY = parentAbsY + parentPadding + Y;

        if (Math.Abs(oldW - Width) > float.Epsilon || Math.Abs(oldH - Height) > float.Epsilon ||
            Math.Abs(oldX - X) > float.Epsilon || Math.Abs(oldY - Y) > float.Epsilon ||
            Math.Abs(oldAbsX - AbsoluteX) > float.Epsilon || Math.Abs(oldAbsY - AbsoluteY) > float.Epsilon)
            OnUpdate?.Invoke();
    }

    public void OverrideAbsolutePositions(float x, float y)
    {
        AbsoluteX = x;
        AbsoluteY = y;
    }
}