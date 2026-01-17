using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Base_Objects;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Cameras;

namespace ThirtyDollarVisualizer.UI.Abstractions;

public class UIContext
{
    protected readonly List<Queue<IRenderable>> LayeredRenderQueue = [];
    public required Camera Camera { get; set; }
    public float ViewportWidth => Camera.Width;
    public float ViewportHeight => Camera.Height;

    public Action<CursorType> RequestCursor { get; set; } = _ => { };

    public void Clear()
    {
        foreach (var queue in LayeredRenderQueue) queue.Clear();
    }

    public void QueueRender(IRenderable renderable, int index)
    {
        while (LayeredRenderQueue.Count <= index) LayeredRenderQueue.Add(new Queue<IRenderable>());

        var queue = LayeredRenderQueue[index];
        queue.Enqueue(renderable);
    }

    public void Render()
    {
        foreach (var queue in CollectionsMarshal.AsSpan(LayeredRenderQueue))
        foreach (var renderable in queue)
            renderable.Render(Camera);

        GL.Scissor(0, 0, (int)ViewportWidth, (int)ViewportHeight);
    }
}

public enum CursorType
{
    Normal,
    Pointer,
    ResizeX,
    ResizeY
}