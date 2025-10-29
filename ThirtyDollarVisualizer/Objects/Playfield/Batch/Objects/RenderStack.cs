using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Base_Objects;
using ThirtyDollarVisualizer.Renderer;
using ThirtyDollarVisualizer.Renderer.Abstract;
using ThirtyDollarVisualizer.Renderer.Buffers;
using ThirtyDollarVisualizer.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;

public class RenderStack<TDataType> : IDisposable where TDataType : unmanaged, IGLReflection, IDebugStringify
{
    public required Shader Shader { get; init; }
    public GLBufferList<TDataType> List { get; }
    public VertexArrayObject VAO { get; }
    
    public RenderStack(int capacity = 0)
    {
        VAO = new VertexArrayObject();
        List = new GLBufferList<TDataType>(capacity);
        
        AddQuadDataToVAO(VAO);
        AddBufferTypeRefectionToVAO(List, VAO);
    }

    protected void AddQuadDataToVAO(VertexArrayObject vao)
    {
        var layout = new VertexBufferLayout()
            .PushFloat(3);
        
        vao.AddBuffer(GLQuad.VBOWithoutUV, layout);
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
        
        GLQuad.EBO.Bind();
        GLQuad.EBO.Update();

        InstancedDrawCall();
    }

    private void InstancedDrawCall()
    {
        GL.DrawElementsInstanced(PrimitiveType.Triangles, GLQuad.EBO.Capacity, DrawElementsType.UnsignedInt, IntPtr.Zero, List.Count);
        Manager.CheckErrors("InstancedDrawCall");
    }

    public void Dispose()
    {
        List.Dispose();
        VAO.Dispose();
        GC.SuppressFinalize(this);
    }
}