using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Engine.Renderer.Abstract;

public interface IPositionable
{
    public Vector3 Position { get; set; }
    public Vector3 Scale { get; set; }
}