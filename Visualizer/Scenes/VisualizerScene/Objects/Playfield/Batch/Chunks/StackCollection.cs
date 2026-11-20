using Sundex.Engine.Renderer;
using OpenTK.Mathematics;
using Shared.Atlases;
using Sundex.Engine.Renderer.Abstract;
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

    public void Render(in RenderContext context)
    {
        // A pooled chunk keeps the stacks of atlases its current slice no longer uses, empty.
        foreach (var (atlas, render_stack) in StaticStacks)
        {
            if (render_stack.List.Count == 0) continue;
            atlas.Bind(context.Commands);
            render_stack.Render(context);
        }

        foreach (var (atlas, render_stack) in AnimatedStacks)
        {
            if (render_stack.List.Count == 0) continue;
            atlas.Bind(context.Commands);
            render_stack.Render(context);
        }
    }
}