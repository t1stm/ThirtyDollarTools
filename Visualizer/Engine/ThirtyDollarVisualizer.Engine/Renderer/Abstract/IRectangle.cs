using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Engine.Renderer.Abstract;

public interface IRectangle : IPositionable
{
    public Vector4 Rectangle { get; set; }

    Vector3 IPositionable.Position
    {
        get => new(Rectangle.X, Rectangle.Y, 0);
        set => Rectangle = new Vector4(value.X, value.Y, Rectangle.Z, Rectangle.W);
    }

    Vector3 IPositionable.Scale
    {
        get => new(Rectangle.Z, Rectangle.W, 1);
        set => Rectangle = new Vector4(Rectangle.X, Rectangle.Y, value.X, value.Y);
    }
}