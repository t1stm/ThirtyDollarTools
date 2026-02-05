using Msdfgen;

namespace ThirtyDollarVisualizer.Engine.Text.Fonts;

public struct TextAlignmentData
{
    public Vector2 Translate;
    public Vector2 Scale;
    public double AdvanceInUnitSpace;

    public void Deconstruct(out double advanceUnitSpace, out Vector2 translate, out Vector2 scale)
    {
        advanceUnitSpace = AdvanceInUnitSpace;
        translate = Translate;
        scale = Scale;
    }
}