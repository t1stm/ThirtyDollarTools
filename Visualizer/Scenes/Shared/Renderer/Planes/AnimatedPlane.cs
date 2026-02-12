using OpenTK.Mathematics;
using Shared.Animations;
using ThirtyDollarVisualizer.Engine.Renderer.Cameras;
using ThirtyDollarVisualizer.Engine.Renderer.Shaders;

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

    public override Vector4 Color => Renderable.Color;
    public override Matrix4 Model => Renderable.Model;
    public override Vector3 Scale => Renderable.Scale;
    public override Vector3 Position => Renderable.Position;
    public override Vector3 Rotation => Renderable.Rotation;
    public override Shader Shader => Renderable.Shader;
    public override Vector3 Translation => Renderable.Translation;
    public override void Render(Camera camera) => Renderable.Render(camera);
    public override void SetShaderUniforms(Camera camera) => Renderable.SetShaderUniforms(camera);
    public override void SetTranslation(Vector3 translation) => Renderable.SetTranslation(translation);

    #endregion
}