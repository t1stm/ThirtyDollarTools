using System.Runtime.ExceptionServices;

namespace ThirtyDollarVisualizer.Engine.Threading;

public class ThreadRunner(Game game)
{
    private readonly Queue<ExceptionDispatchInfo> _exceptions = [];
    public Game Game { get; } = game;

    public Thread RunThread(Action action)
    {
        return RunThread(action, AddException);
    }

    public Task RunTask(Action action)
    {
        return Task.Run(() => { ActionWrapper(action, AddException); });
    }

    public void AddException(ExceptionDispatchInfo exception)
    {
        lock (_exceptions)
        {
            _exceptions.Enqueue(exception);
        }
    }

    public void Update()
    {
        lock (_exceptions)
        {
            while (_exceptions.TryDequeue(out var exception)) exception.Throw();
        }
    }

    private static Thread RunThread(Action action, Action<ExceptionDispatchInfo> exceptionHandler)
    {
        var thread = new Thread(() => { ActionWrapper(action, exceptionHandler); });

        thread.Start();
        return thread;
    }

    private static void ActionWrapper(Action action, Action<ExceptionDispatchInfo> exceptionHandler)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            exceptionHandler(ExceptionDispatchInfo.Capture(e));
        }
    }
}