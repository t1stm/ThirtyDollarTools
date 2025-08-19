using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Objects.Playfield.Atlas;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch;

public class PlayfieldChunk(int size)
{
    protected readonly Lazy<VertexArrayObject> SoundsVAO = new(() => GetVAO(size));
    protected PlayfieldSound[] Sounds = [];

    private static unsafe VertexArrayObject GetVAO(int size)
    {
        var vao = new VertexArrayObject();
        vao.Bind();

        var soundBuffer = new BufferObject<PlayfieldSound>(size, BufferTarget.ArrayBuffer);
        vao.AddBuffer(soundBuffer, 
            new VertexBufferLayout()
                .PushMatrix4(1) // Model
                .PushFloat(1, true) // Alpha
            );
        
        var uvBuffer = new BufferObject<QuadUV>(size, BufferTarget.ArrayBuffer);
        vao.AddBuffer(uvBuffer, new VertexBufferLayout()
            .PushFloat(2));
        
        return vao;
    }
    
    public void Render()
    {
        var vao = SoundsVAO.Value;
        vao.Bind();
        
        
    }
}