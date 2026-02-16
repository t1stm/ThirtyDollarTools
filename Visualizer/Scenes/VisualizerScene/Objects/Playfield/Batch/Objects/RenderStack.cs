using OpenTK.Graphics.OpenGL;
using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Buffers;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Renderer.Queues;
using Sundex.Engine.Renderer.Shaders;

namespace VisualizerScene.Objects.Playfield.Batch.Objects;

public class RenderStack<TDataType> : IDisposable where TDataType : unmanaged, IGPUReflection
{
    public RenderStack(DeleteQueue deleteQueue, int capacity = 0,
        GLBuffer<float>? initialVertexBuffer = null,
        VertexBufferLayout? initialLayout = null,
        GLBuffer<int>? initialIndexElementBuffer = null)
    {
        initialVertexBuffer ??= GLQuad.VBOWithoutUV;
        initialLayout ??= new VertexBufferLayout().PushFloat(3);
        initialIndexElementBuffer ??= GLQuad.EBO;

        VAO = new VertexArrayObject();
        List = new GLBufferList<TDataType>(deleteQueue, capacity);

        AddQuadDataToVAO(VAO, initialVertexBuffer, initialLayout, initialIndexElementBuffer);
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

    protected void AddQuadDataToVAO(VertexArrayObject vao, GLBuffer<float> vertexBuffer,
        VertexBufferLayout initialLayout, GLBuffer<int> indexBuffer)
    {
        vao.AddBuffer(vertexBuffer, initialLayout);
        vao.SetIndexBuffer(indexBuffer);
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