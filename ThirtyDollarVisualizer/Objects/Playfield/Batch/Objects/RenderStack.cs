using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Engine.Renderer;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Buffers;
using ThirtyDollarVisualizer.Engine.Renderer.Cameras;
using ThirtyDollarVisualizer.Engine.Renderer.Queues;
using ThirtyDollarVisualizer.Engine.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;

public class RenderStack<TDataType> : IDisposable where TDataType : unmanaged, IGPUReflection
{
    public RenderStack(DeleteQueue deleteQueue, int capacity = 0)
    {
        VAO = new VertexArrayObject();
        List = new GLBufferList<TDataType>(deleteQueue, capacity);

        AddQuadDataToVAO(VAO);
        AddBufferTypeRefectionToVAO(List, VAO);
    }

    public required Shader Shader { get; init; }
    public GLBufferList<TDataType> List { get; }
    public VertexArrayObject VAO { get; }

    public void Dispose()
    {
        List.Dispose();
        VAO.Dispose();
        GC.SuppressFinalize(this);
    }

    protected void AddQuadDataToVAO(VertexArrayObject vao)
    {
        var layout = new VertexBufferLayout()
            .PushFloat(3);

        vao.AddBuffer(GLQuad.VBOWithoutUV, layout);
        vao.SetIndexBuffer(GLQuad.EBO);
    }

    protected void AddBufferTypeRefectionToVAO(IGPUBuffer<TDataType> buffer, VertexArrayObject vao)
    {
        var layout = new VertexBufferLayout();

        TDataType.SelfReflectToGL(layout);
        vao.AddBuffer(buffer, layout);
    }

    public void Render(Camera camera)
    {
        Shader.Use();
        Shader.SetUniform("u_VPMatrix", camera.GetVPMatrix());

        VAO.Bind();
        VAO.Update();

        InstancedDrawCall();
    }

    private void InstancedDrawCall()
    {
        GL.DrawElementsInstanced(PrimitiveType.Triangles, GLQuad.EBO.Capacity, DrawElementsType.UnsignedInt,
            IntPtr.Zero, List.Count);
    }
}