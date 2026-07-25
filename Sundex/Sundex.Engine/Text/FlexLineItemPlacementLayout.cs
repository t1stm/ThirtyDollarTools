using OpenTK.Mathematics;

namespace Sundex.Engine.Text;

public struct FlexLineItemPlacementLayout()
{
    public double Advance = 0;
    public Vector2 Translate = Vector2.Zero;
    public Vector2 Scale = Vector2.Zero;
    public int NewLines = 0;

    public void Deconstruct(out double advance, out Vector2 translate, out Vector2 scale)
    {
        advance = Advance;
        translate = Translate;
        scale = Scale;
    }
}