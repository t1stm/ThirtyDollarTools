using System.Collections.Concurrent;

namespace ThirtyDollarVisualizer.Renderer;

/// <summary>
/// A basic queue for actions that need to execute before we start drawing.
/// </summary>
public class RenderThreadTaskQueue
{
    private readonly ConcurrentQueue<Action> _queue = new();
    
    /// <summary>
    /// Enqueueues an action to the render task queue.
    /// </summary>
    /// <param name="action">The action to execute before running the rest of the render thread.</param>
    public void Enqueue(Action action)
    {
        _queue.Enqueue(action);
    }

    /// <summary>
    /// Runs all enqueued tasks.
    /// </summary>
    public void RunTasks()
    {
        while(_queue.TryDequeue(out var action))
            action.Invoke();
    }
}