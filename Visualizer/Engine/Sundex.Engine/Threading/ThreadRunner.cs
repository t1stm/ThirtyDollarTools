using System.Runtime.ExceptionServices;
using Serilog;

namespace Sundex.Engine.Threading;

public class ThreadRunner(ILogger logger)
{
    private readonly Queue<ExceptionDispatchInfo> _exceptions = [];
    public ILogger Logger { get; } = logger;

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