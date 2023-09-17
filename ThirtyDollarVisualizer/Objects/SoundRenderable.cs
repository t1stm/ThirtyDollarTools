using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.Objects;

public class SoundRenderable : TexturedPlane
{
    public SoundRenderable(Texture texture, Vector3 position, Vector2 width_height) : base(texture, position, width_height)
    {
        // TODO: extend the textured plane by adding the animations as a render object.
    }
}