namespace ThirtyDollarVisualizer.Engine.Next;

public class NextEngine
{
    private readonly Thread AudioThread;
    private readonly Thread UpdateThread;
    private readonly Thread DrawThread;
    
    public Action? OnAudio { get; set; }
    public Action? OnUpdate { get; set; }
    public Action? OnDraw { get; set; }

    public int ThreadSleepTime { get; set; } = 1;
    public bool ShouldClose { get; set; }

    public NextEngine(string initial_window_title = "NextEngine")
    {
        AudioThread = new Thread(() => ThreadAction(OnAudio));
        UpdateThread = new Thread(() => ThreadAction(OnUpdate));
        DrawThread = new Thread(() => ThreadAction(OnDraw));
    }

    private void ThreadAction(Action? action)
    {
        while (!ShouldClose)
        {
            action?.Invoke();
            Thread.Sleep(ThreadSleepTime);
        }
    }

    public void Run()
    {
        UpdateThread.Start();
        AudioThread.Start();
        DrawThread.Start();
    }

    public void Stop()
    {
        ShouldClose = true;
        UpdateThread.Join();
        AudioThread.Join();
        DrawThread.Join();
    }
}