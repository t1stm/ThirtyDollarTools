using OpenTK.Mathematics;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Enums;

namespace VisualizerScene.Objects.Sound_Values;

public interface ISoundValue : IPositionable
{
    public PositionAlign PositionAlign { get; set; }
    public Vector3 Translation { get; set; }
    public float ScaleMultiplier { get; set; }

    public void UpdatePosition();
    public void Reset();
}