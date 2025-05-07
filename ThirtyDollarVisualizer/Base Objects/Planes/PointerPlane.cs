using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Renderer.Instanced;

namespace ThirtyDollarVisualizer.Objects.Planes;

public class PointerPlane(Quad quad, int index, QuadArray parent) : Renderable
{
    private Quad _backingQuad = quad;
    
    public override Matrix4 Model
    {
        get => base.Model;
        set
        {
            base.Model = value;
            _backingQuad.Model = value;
            parent.SetDirty(index, _backingQuad);
        }
    }

    public override Vector4 Color
    {
        get => base.Color;
        set
        {
            base.Color = value;
            _backingQuad.Color = value;
            parent.SetDirty(index, _backingQuad);
        } 
    }
}