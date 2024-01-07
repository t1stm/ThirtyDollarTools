using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.Objects;

public class MidiKey : ColoredPlane
{
    public Vector4 BorderColor = Vector4.Zero;
    public float BorderSizePx = 0f;
    
    public MidiKey(Vector4 color, Vector3 position, Vector2 width_height, float border_radius = 0) : base(color, position, width_height, border_radius)
    {
    }

    public MidiKey(Vector4 color, Vector3 position, Vector2 width_height, Shader? shader) : base(color, position, width_height, shader)
    {
    }

    public override void SetShaderUniforms(Camera camera)
    {
        base.SetShaderUniforms(camera);
        Shader.SetUniform("u_BorderColor", BorderColor);
        Shader.SetUniform("u_BorderSizePx", BorderSizePx);
    }
}