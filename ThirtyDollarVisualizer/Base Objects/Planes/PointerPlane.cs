using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Renderer.Instanced;

namespace ThirtyDollarVisualizer.Objects.Planes;

/// <summary>
/// A plane that points to an already allocated <see cref="Quad"/> struct contained in a <see cref="QuadArray"/>.
/// </summary>
/// <param name="parent">The Quad's holder (c# doesn't support passing refs)</param>
/// <param name="index">The index of the quad.</param>
public class PointerPlane(QuadArray parent, int index) : Renderable
{
    private QuadArray _parent = parent;

    public override Matrix4 Model
    {
        get => base.Model;
        set
        {
            base.Model = value;
            ref var quad = ref _parent[index];
            quad.Model = value;
            _parent.SetDirty(index);
        }
    }

    public override Vector4 Color
    {
        get => base.Color;
        set
        {
            base.Color = value;
            ref var quad = ref _parent[index];
            quad.Color = value;
            _parent.SetDirty(index);
        } 
    }
}