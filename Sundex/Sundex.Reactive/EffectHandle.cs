namespace Sundex.Reactive;

/// <summary>
///     A side effect that reruns when what it read changes. Each run gets a scope of its own,
///     ended before the next run, so effects, computeds and timers made inside a run don't pile up.
///     Made with <see cref="Rx.Effect" />; called a handle because <c>Effect(...)</c> is the helper.
/// </summary>
public sealed class EffectHandle : Reaction, IDisposable
{
    private readonly Action _run;
    private readonly Scope? _owner;
    private Scope? _runScope;
    private List<Action>? _cleanups;
    private bool _disposed;
    private bool _queued;

    internal EffectHandle(Action run, Scope? owner)
    {
        _run = run;
        _owner = owner;
    }

    public bool Disposed => _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unsubscribe();
        Clean();
        _runScope?.Dispose();
        _runScope = null;
    }

    internal override void MarkDirty()
    {
        if (_queued || _disposed) return;
        _queued = true;
        Rx.Enqueue(this);
    }

    /// <summary>From the queue: told something changed, so it checks before doing the work.</summary>
    internal void RunIfChanged()
    {
        _queued = false;
        if (!_disposed && AnySourceChanged()) Run();
    }

    internal void Run()
    {
        _queued = false;
        if (_disposed) return;
        Unsubscribe();
        Clean();
        _runScope?.Dispose();
        _runScope = Scope.ChildOf(_owner);

        var previous = Rx.Observer;
        var previousEffect = Rx.CurrentEffect;
        var previousScope = Scope.Current;
        Rx.Observer = this;
        Rx.CurrentEffect = this;
        Scope.Current = _runScope;
        try
        {
            _run();
        }
        catch (Exception exception)
        {
            Rx.OnError(exception, "an effect");
        }
        finally
        {
            Rx.Observer = previous;
            Rx.CurrentEffect = previousEffect;
            Scope.Current = previousScope;
        }
    }

    internal void AddCleanup(Action cleanup) => (_cleanups ??= []).Add(cleanup);

    private void Clean()
    {
        if (_cleanups == null) return;
        var cleanups = _cleanups;
        _cleanups = null;
        foreach (var cleanup in cleanups)
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                Rx.OnError(exception, "an effect's cleanup");
            }
    }
}
