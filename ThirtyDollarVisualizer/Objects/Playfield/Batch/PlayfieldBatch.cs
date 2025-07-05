using System.Buffers;
using System.Runtime.InteropServices;
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

        var rentBytes = ArrayPool<byte>.Shared.Rent(size * sizeof(PlayfieldSound));
        var rentSounds = MemoryMarshal.Cast<byte, PlayfieldSound>(rentBytes.AsSpan(0, size * sizeof(PlayfieldSound)));
        rentSounds.Clear();

        var bufferObject = new BufferObject<PlayfieldSound>(rentSounds, BufferTarget.ArrayBuffer);
        ArrayPool<byte>.Shared.Return(rentBytes);
        
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
        
    }
}