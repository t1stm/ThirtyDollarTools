using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Engine.Renderer.Debug;
using ThirtyDollarVisualizer.Engine.Renderer.Enums;

namespace ThirtyDollarVisualizer.Engine.Renderer.Queues;

public class DeleteQueue
{
    private readonly Queue<(DeleteType type, int handle)> _queue = new();
    
    public void Enqueue(DeleteType type, int handle)
    {
        lock (_queue) _queue.Enqueue((type, handle));
    }

    public void ExecuteDeletes()
    {
        lock (_queue)
        {
            while (_queue.TryDequeue(out var tuple))
            {
                var (type, handle) = tuple;
                if (handle == 0) continue;
                switch (type)
                {
                    case DeleteType.VBO:
                    case DeleteType.IBO:
                        GL.DeleteBuffer(handle);
                        break;
                    case DeleteType.VAO:
                        GL.DeleteVertexArray(handle);
                        break;
                    case DeleteType.Texture:
                        GL.DeleteTexture(handle);
                        break;
                    case DeleteType.Shader:
                        GL.DeleteShader(handle);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                RenderMarker.Debug($"Deleted {type}: ({handle})");
            }
        }
    }
}