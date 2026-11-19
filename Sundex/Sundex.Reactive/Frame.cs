using System.Collections.Concurrent;
using System.Diagnostics;

namespace Sundex.Reactive;

/// <summary>
///     The UI thread's run loop: a <see cref="SynchronizationContext" /> so <c>await</c> comes back
///     to it, timers, per-frame tickers, and the effect flush. The host calls <see cref="Install" />
///     once on the UI thread and <see cref="Pump" /> once per frame.
/// </summary>
public static class Frame
{
    private static readonly ConcurrentQueue<Action> Posted = new();
    private static readonly List<Timer> Timers = [];
    private static readonly List<Func<bool>> Tickers = [];
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static Thread? _uiThread;

    public static int TimerCount => Timers.Count;
    public static int TickerCount => Tickers.Count;

    /// <summary>How long the last <see cref="Pump" /> took, and the flush that ends it: a probe's frame timings.</summary>
    public static double LastPumpMs { get; private set; }

    public static double LastFlushMs { get; private set; }
    public static int PostedCount => Posted.Count;

    /// <summary>Milliseconds since start.</summary>
    public static double Now => Clock.Elapsed.TotalMilliseconds;

    /// <summary>How long the posted actions of one <see cref="Pump" /> may take before the rest wait a frame.</summary>
    public static double PostedBudgetMs { get; set; } = 8;

    /// <summary>Makes the calling thread the UI thread.</summary>
    public static void Install()
    {
        _uiThread = Thread.CurrentThread;
        SynchronizationContext.SetSynchronizationContext(new FrameContext());
    }

    /// <summary>Forgets the UI thread: for tests, and for a host shutting down.</summary>
    public static void Uninstall()
    {
        if (ReferenceEquals(Thread.CurrentThread, _uiThread)) SynchronizationContext.SetSynchronizationContext(null);
        _uiThread = null;
    }

    public static bool OnUiThread => _uiThread == null || ReferenceEquals(Thread.CurrentThread, _uiThread);

    /// <summary>Debug builds only: state is UI-thread only, and a write from elsewhere is a bug.</summary>
    [Conditional("DEBUG")]
    public static void AssertUiThread()
    {
        if (!OnUiThread)
            throw new InvalidOperationException(
                "Reactive state was written off the UI thread. Post the write with Frame.Post.");
    }

    /// <summary>Runs <paramref name="action" /> on the UI thread at the next <see cref="Pump" />. Safe from any thread.</summary>
    public static void Post(Action action) => Posted.Enqueue(action);

    /// <summary><c>setTimeout</c>, ended with the current scope.</summary>
    public static IDisposable After(double milliseconds, Action action)
    {
        var timer = new Timer(Now + milliseconds, 0, action);
        Timers.Add(timer);
        Scope.Current?.OnDispose(timer.Dispose);
        return timer;
    }

    /// <summary><c>setInterval</c>, ended with the current scope.</summary>
    public static IDisposable Every(double milliseconds, Action action)
    {
        var timer = new Timer(Now + milliseconds, milliseconds, action);
        Timers.Add(timer);
        Scope.Current?.OnDispose(timer.Dispose);
        return timer;
    }

    /// <summary>Runs every frame until it returns false or the current scope ends.</summary>
    public static void Tick(Func<bool> ticker)
    {
        var scope = Scope.Current;
        Tickers.Add(scope == null ? ticker : () => !scope.Disposed && ticker());
    }

    /// <summary>Posted actions, timers, tickers, then the effect flush.</summary>
    public static void Pump()
    {
        var budget = Stopwatch.StartNew();
        // A burst of worker results must not take a whole frame: the rest wait for the next one.
        while (budget.Elapsed.TotalMilliseconds < PostedBudgetMs && Posted.TryDequeue(out var action))
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Rx.OnError(exception, "a posted action");
            }

        var now = Now;
        for (var index = Timers.Count - 1; index >= 0; index--)
        {
            var timer = Timers[index];
            if (timer.Disposed)
            {
                Timers.RemoveAt(index);
                continue;
            }

            if (now < timer.Due) continue;
            if (timer.Interval > 0) timer.Due = now + timer.Interval;
            else Timers.RemoveAt(index);
            try
            {
                timer.Action();
            }
            catch (Exception exception)
            {
                Rx.OnError(exception, "a timer");
            }
        }

        for (var index = Tickers.Count - 1; index >= 0; index--)
        {
            bool keep;
            try
            {
                keep = Tickers[index]();
            }
            catch (Exception exception)
            {
                Rx.OnError(exception, "a ticker");
                keep = false;
            }

            if (!keep) Tickers.RemoveAt(index);
        }

        var flushing = budget.Elapsed.TotalMilliseconds;
        Rx.Flush();
        LastPumpMs = budget.Elapsed.TotalMilliseconds;
        LastFlushMs = LastPumpMs - flushing;
    }

    private sealed class Timer(double due, double interval, Action action) : IDisposable
    {
        public double Due = due;
        public double Interval { get; } = interval;
        public Action Action { get; } = action;
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private sealed class FrameContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => Posted.Enqueue(() => d(state));

        public override void Send(SendOrPostCallback d, object? state) =>
            throw new NotSupportedException("Nothing waits on the UI thread.");

        public override SynchronizationContext CreateCopy() => this;
    }
}

/// <summary>Fire-and-forget <c>async</c> for event handlers, with the error reported rather than lost.</summary>
public static class Async
{
    public static void Run(Func<Task> work) => _ = Guard(work);

    private static async Task Guard(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Rx.OnError(exception, "an async handler");
        }
    }
}
