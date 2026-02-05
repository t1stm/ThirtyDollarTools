using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Enums;

namespace ThirtyDollarVisualizer.Objects.Sound_Values;

public interface ISoundValue : IPositionable
{
    public PositionAlign PositionAlign { get; set; }
    public Vector3 Translation { get; set; }
    public float ScaleMultiplier { get; set; }

    public void UpdatePosition();
    public void Reset();
}