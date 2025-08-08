using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch;

public class PlayfieldBatch
{
    protected Lazy<VertexArrayObject> SoundsVAO;
    
    public PlayfieldBatch(int size)
    {
        SoundsVAO = new Lazy<VertexArrayObject>(() => GetVAO(size));
    }
    
    private static unsafe VertexArrayObject GetVAO(int size)
    {
        var vao = new VertexArrayObject();
        vao.Bind();

        var bufferObject = new BufferObject<PlayfieldSound>(size, BufferTarget.ArrayBuffer);
        vao.AddBuffer(bufferObject, 
            new VertexBufferLayout()
                .PushMatrix4(1) // Model
                .PushFloat(2, true) // UV
                .PushFloat(1, true) // Alpha
            );
        
        return vao;
    }
    
    public void Render()
    {
        var vao = SoundsVAO.Value;
        vao.Bind();
        
        
    }
}