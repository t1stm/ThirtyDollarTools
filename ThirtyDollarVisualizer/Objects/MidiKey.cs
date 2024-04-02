using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Animations;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.Objects;

public class MidiKey : ColoredPlane
{
    private readonly MidiKeyPressAnimation PressAnimation;
    public Vector4 BorderColor = Vector4.Zero;
    public float BorderSizePx = 0f;

    public MidiKey(Vector4 color, Vector3 position, Vector3 scale, float border_radius = 0) : base(color, position,
        scale, border_radius)
    {
        PressAnimation = new MidiKeyPressAnimation(133, () => { UpdateModel(false); })
        {
            ReleasedColor = color,
            AffectsChildren = false
        };
        Shader = new Shader("ThirtyDollarVisualizer.Assets.Shaders.bordered.vert",
            "ThirtyDollarVisualizer.Assets.Shaders.bordered.frag");
    }

    public MidiKey(Vector4 color, Vector3 position, Vector3 scale, Shader? shader) : base(color, position, scale,
        shader)
    {
        PressAnimation = new MidiKeyPressAnimation(133, () => { UpdateModel(false); })
        {
            ReleasedColor = color
        };
        Shader = new Shader("ThirtyDollarVisualizer.Assets.Shaders.bordered.vert",
            "ThirtyDollarVisualizer.Assets.Shaders.bordered.frag");
    }

    public void Press(long length_ms)
    {
        PressAnimation.PressedColor = BorderColor;
        PressAnimation.PressedLength = length_ms;
        PressAnimation.StartAnimation();
    }

    public override void Render(Camera camera)
    {
        if (PressAnimation.IsRunning) UpdateModel(false, PressAnimation);
        base.Render(camera);
    }

    public override void SetShaderUniforms(Camera camera)
    {
        base.SetShaderUniforms(camera);
        Shader.SetUniform("u_BorderColor", BorderColor);
        Shader.SetUniform("u_BorderSizePx", BorderSizePx);
    }
}