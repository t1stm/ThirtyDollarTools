namespace Sundex.Reactive;

/// <summary>
///     The lifetime of one piece of UI: its effects, computeds, timers and in-flight requests end
///     together when it does. A scope made while another is current is that one's child and ends
///     with it.
/// </summary>
public sealed class Scope : IDisposable
{
    private readonly CancellationTokenSource _cancel = new();
    private readonly Scope? _parent;
    private readonly Action _detach;
    private List<Action>? _disposers;
    private bool _disposed;

    /// <summary>A scope owned by <see cref="Current" />, or a root when there is none.</summary>
    public Scope() : this(Current)
    {
    }

    private Scope(Scope? parent)
    {
        _parent = parent;
        _detach = Dispose;
        _parent?.OnDispose(_detach);
    }

    /// <summary>A scope with no owner: ended only by its own <see cref="Dispose" />.</summary>
    public static Scope Root() => new(null);

    /// <summary>A scope owned by <paramref name="parent" />.</summary>
    public static Scope ChildOf(Scope? parent) => new(parent);

    /// <summary>The scope that new effects, computeds and timers belong to.</summary>
    public static Scope? Current { get; internal set; }

    /// <summary>Cancelled when the scope ends: hand it to any request the scope starts.</summary>
    public CancellationToken Token => _cancel.Token;

    public bool Disposed => _disposed;

    /// <summary>Runs <paramref name="action" /> when the scope ends, or now if it already has.</summary>
    public void OnDispose(Action action)
    {
        if (_disposed) action();
        else (_disposers ??= []).Add(action);
    }

    /// <summary>Runs <paramref name="build" /> with this scope current and nothing tracked.</summary>
    public T Run<T>(Func<T> build)
    {
        var previous = Current;
        Current = this;
        try
        {
            return Rx.Untrack(build);
        }
        finally
        {
            Current = previous;
        }
    }

    /// <inheritdoc cref="Run{T}" />
    public void Run(Action build) => Run(() =>
    {
        build();
        return 0;
    });

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Cancelled, not disposed: a request that outlives the scope still reads its token.
        _cancel.Cancel();
        // A long-lived parent mustn't hold every child it has ever had.
        _parent?._disposers?.Remove(_detach);
        if (_disposers == null) return;
        var disposers = _disposers;
        _disposers = null;
        for (var index = disposers.Count - 1; index >= 0; index--)
            try
            {
                disposers[index]();
            }
            catch (Exception exception)
            {
                Rx.OnError(exception, "a scope's cleanup");
            }
    }
}
