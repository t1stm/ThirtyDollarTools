using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Base_Objects;
using ThirtyDollarVisualizer.Renderer;
using ThirtyDollarVisualizer.Renderer.Abstract;
using ThirtyDollarVisualizer.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;

public class RenderStack<TDataType> : IDisposable where TDataType : unmanaged, IGLReflection
{
    public required Shader Shader { get; init; }
    public GLBufferList<TDataType> List { get; }
    public VertexArrayObject VAO { get; }
    public Action<RenderStack<TDataType>>? BeforeRender { get; set; }

    public RenderStack(ReadOnlySpan<TDataType> data)
    {
        VAO = new VertexArrayObject();
        List = new GLBufferList<TDataType>();
        
        AddBufferTypeRefectionToVAO(List, VAO);
        List.SetBufferData(data);
    }

    protected void AddBufferTypeRefectionToVAO(IGLBuffer<TDataType> buffer, VertexArrayObject vao)
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
        GL.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero, List.Count);
    }

    public void Dispose()
    {
        Shader.Dispose();
        List.Dispose();
        VAO.Dispose();
        GC.SuppressFinalize(this);
    }
}