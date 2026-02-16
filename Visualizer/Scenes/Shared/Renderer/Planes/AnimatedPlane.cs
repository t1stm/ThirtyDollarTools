using OpenTK.Mathematics;
using Shared.Animations;
using Sundex.Core.Animations;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Renderer.Shaders;

namespace Shared.Renderer.Planes;

public class AnimatedPlane<T>(T renderable, Animation animation) : Renderable
    where T : Renderable
{
    public T Renderable { get; } = renderable;
    public Animation Animation { get; } = animation;
    
    public override void Update()
    {
        Renderable.UpdateModel(false, [Animation]);
        Renderable.Update();
    }
    
    #region Renderable Passthrough

    public override Vector4 Color
    {
        get => Renderable.Color;
        set => Renderable.Color = value;
    }

    public override Matrix4 Model
    {
        get => Renderable.Model;
        set => Renderable.Model = value;
    }

    public override Vector3 Scale
    {
        get => Renderable.Scale;
        set => Renderable.Scale = value;
    }

    public override Vector3 Position
    {
        get => Renderable.Position;
        set => Renderable.Position = value;
    }

    public override Vector3 Rotation
    {
        get => Renderable.Rotation;
        set => Renderable.Rotation = value;
    }

    public override Shader Shader
    {
        get => Renderable.Shader;
        set => Renderable.Shader = value;
    }

    public override Vector3 Translation
    {
        get => Renderable.Translation;
        set => Renderable.Translation = value;
    }

    public override void Render(Camera camera) => Renderable.Render(camera);
    public override void SetShaderUniforms(Camera camera) => Renderable.SetShaderUniforms(camera);
    public override void SetTranslation(Vector3 translation) => Renderable.SetTranslation(translation);

    #endregion
}