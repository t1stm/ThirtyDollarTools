using System.Runtime.InteropServices;
using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.UI;

public class UIContext
{
    protected readonly List<Queue<Renderable>> LayeredRenderQueue = [];
    public required Camera Camera { get; set; }
    public float ViewportWidth => Camera.Width;
    public float ViewportHeight => Camera.Height;

    public Action<CursorType> RequestCursor { get; set; } = _ => { };

    public void Clear()
    {
        foreach (var queue in LayeredRenderQueue)
        {
            queue.Clear();
        }
    }
    
    public void QueueRender(Renderable renderable, int index)
    {
        while (LayeredRenderQueue.Count <= index)
        {
            LayeredRenderQueue.Add(new Queue<Renderable>());
        }
        
        var queue = LayeredRenderQueue[index];
        queue.Enqueue(renderable);
    }

    public void Render()
    {
        foreach (var queue in CollectionsMarshal.AsSpan(LayeredRenderQueue))
        {
            foreach (var renderable in queue)
            {
                renderable.Render(Camera);
            }
        }
    }
}

public enum CursorType
{
    Normal,
    Pointer
}