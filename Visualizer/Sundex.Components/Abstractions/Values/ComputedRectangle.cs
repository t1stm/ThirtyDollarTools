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
        Width = current.Width.Resolve(parent?.Computed.Width ?? current.Context.ViewportWidth);
        Height = current.Height.Resolve(parent?.Computed.Height ?? current.Context.ViewportHeight);

        X = current.X.Resolve(Width);
        Y = current.Y.Resolve(Height);
        
        AbsoluteX = X + parent?.Computed.X ?? 0;
        AbsoluteY = Y + parent?.Computed.Y ?? 0;
    }
}