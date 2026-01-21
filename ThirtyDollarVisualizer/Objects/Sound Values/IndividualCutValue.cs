using OpenTK.Mathematics;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Cameras;
using ThirtyDollarVisualizer.Engine.Renderer.Enums;

namespace ThirtyDollarVisualizer.Objects.Sound_Values;

public class IndividualCutValue(IndividualCutEvent ice) : ISoundValue, IRenderable
{
    public Vector3 Position { get; set; }
    public Vector3 Scale { get; set; }
    
    public PositionAlign PositionAlign { get; set; } = PositionAlign.Top | PositionAlign.Left;
    public Vector3 Translation { get; set; }
    public float ScaleMultiplier { get; set; }
    
    public void UpdatePosition()
    {
        
    }

    public void Reset()
    {
        throw new NotImplementedException();
    }

    public void Render(Camera camera)
    {
        throw new NotImplementedException();
    }
}