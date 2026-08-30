using OpenTK.Mathematics;
using Shared.Atlases;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Cameras;
using VisualizerScene.Objects.Playfield.Batch.Objects;

namespace VisualizerScene.Objects.Playfield.Batch.Chunks;

public class StackCollection : IDisposable, IRenderable, IClippable
{
    public Dictionary<StaticSoundAtlas, RenderStack<StaticSound>> StaticStacks { get; set; } = [];
    public Dictionary<FramedAtlas, RenderStack<SoundData>> AnimatedStacks { get; set; } = [];

    /// <summary>Lets the collection live inside a ScrollView (UIContext applies the scissor).</summary>
    public Vector4i? ClipRect { get; set; }

    public void Dispose()
    {
        foreach (var (_, buffer_object) in StaticStacks) buffer_object.Dispose();
        foreach (var (_, buffer_object) in AnimatedStacks) buffer_object.Dispose();

        StaticStacks.Clear();
        AnimatedStacks.Clear();

        GC.SuppressFinalize(this);
    }

    public void Render(Camera temporaryCamera)
    {
        foreach (var (atlas, render_stack) in StaticStacks)
        {
            atlas.Bind();
            render_stack.Render(temporaryCamera);
        }

        foreach (var (atlas, render_stack) in AnimatedStacks)
        {
            atlas.Bind();
            render_stack.Shader.Use(); // one bind per stack, rather than grouping stacks by shader

            atlas.SetUniforms(render_stack.Shader);
            render_stack.Render(temporaryCamera);
        }
    }
}