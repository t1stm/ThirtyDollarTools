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
    
    // TODO this explodes when the parent has a padding set. currently there is no way to signal that the parent has this. i'll probably need to add an interface called IPositioningElement or something like that
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

        X = current.X.Resolve(Width);
        Y = current.Y.Resolve(Height);

        AbsoluteX = X + parent?.Computed.X ?? 0;
        AbsoluteY = Y + parent?.Computed.Y ?? 0;
    }
}