using System.Runtime.CompilerServices;
using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Buffers;
using Sundex.Engine.Renderer.Shaders;

namespace VisualizerScene.Objects.Playfield.Batch.Objects;

public class RenderStack<TDataType>(int capacity = 0, bool texturedQuad = false) : IDisposable
    where TDataType : unmanaged, IVertexData
{
    private static readonly ConditionalWeakTable<Shader, Pipeline> PositionPipelines = new();
    private static readonly ConditionalWeakTable<Shader, Pipeline> TexturedPipelines = new();
    private Pipeline? _pipeline;

    public required Shader Shader { get; init; }
    public GpuBufferList<TDataType> List { get; } = new(capacity);

    public void Dispose()
    {
        List.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Render(in RenderContext context)
    {
        if (List.Count == 0) return;
        _pipeline ??= texturedQuad
            ? TexturedPipelines.GetValue(Shader, static shader => CreatePipeline(shader, true))
            : PositionPipelines.GetValue(Shader, static shader => CreatePipeline(shader, false));

        var commands = context.Commands;
        commands.SetPipeline(_pipeline);
        commands.SetVertexBuffer(0, texturedQuad ? UnitQuad.PositionsWithUV : UnitQuad.Positions);
        commands.SetVertexBuffer(1, List.Buffer);
        commands.SetIndexBuffer(UnitQuad.Indices);
        commands.DrawIndexed(UnitQuad.IndexCount, List.Count);
    }

    private static Pipeline CreatePipeline(Shader shader, bool textured)
    {
        var vertices = new VertexBufferLayout().PushFloat(3);
        if (textured) vertices.PushFloat(2);
        var instances = new VertexBufferLayout(VertexStepRate.Instance);
        TDataType.DescribeLayout(instances);
        return new Pipeline(new PipelineDescription(shader, [vertices, instances]));
    }
}
