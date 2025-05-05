using System.Collections.Concurrent;

namespace ThirtyDollarVisualizer.Renderer;

public class RenderThreadTaskQueue
{
    private readonly ConcurrentQueue<Action> _queue = new();
    
    public void Enqueue(Action action)
    {
        _queue.Enqueue(action);
    }

    public void RunTasks()
    {
        while(_queue.TryDequeue(out var action))
            action.Invoke();
    }
}