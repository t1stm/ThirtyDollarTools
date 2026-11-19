namespace Sundex.Reactive;

/// <summary>
///     The helpers reactive code is written with. Meant for <c>using static Sundex.Reactive.Rx;</c>,
///     which makes <c>State(…)</c>, <c>Computed(…)</c> and <c>Effect(…)</c> read like keywords.
/// </summary>
public static class Rx
{
    private static readonly Queue<EffectHandle> Pending = new();
    private static bool _flushing;

    internal static Reaction? Observer { get; set; }
    internal static EffectHandle? CurrentEffect { get; set; }

    /// <summary>Where an effect's, a cleanup's or a posted action's exception goes. Replace it with the app's logger.</summary>
    public static Action<Exception, string> OnError { get; set; } =
        (exception, where) => Console.Error.WriteLine($"[Sundex.Reactive] {where} threw: {exception}");

    /// <summary>Effects waiting for the next <see cref="Flush" />.</summary>
    public static int PendingCount => Pending.Count;

    public static State<T> State<T>(T value) => new(value);

    /// <summary>A computed owned by the current scope: it lets go of its sources when the scope ends.</summary>
    public static Computed<T> Computed<T>(Func<T> compute)
    {
        var computed = new Computed<T>(compute);
        Scope.Current?.OnDispose(computed.Release);
        return computed;
    }

    /// <summary>
    ///     Runs <paramref name="run" /> now, then again whenever something it read changes, at most
    ///     once per <see cref="Flush" />. Owned by the current scope.
    /// </summary>
    public static EffectHandle Effect(Action run)
    {
        var owner = Scope.Current;
        var effect = new EffectHandle(run, owner);
        owner?.OnDispose(effect.Dispose);
        effect.Run();
        return effect;
    }

    /// <summary>Inside an effect: runs before its next run, and when it is disposed.</summary>
    public static void OnCleanup(Action cleanup)
    {
        if (CurrentEffect != null) CurrentEffect.AddCleanup(cleanup);
        else Scope.Current?.OnDispose(cleanup);
    }

    /// <summary>
    ///     An async value that reloads when what <paramref name="load" /> read before its first
    ///     <c>await</c> changes, cancelling the load it replaces. Owned by the current scope.
    /// </summary>
    public static Resource<T> Load<T>(Func<CancellationToken, Task<T>> load, T initial) => new(load, initial);

    /// <summary>
    ///     Runs <paramref name="build" /> with no current scope, so what it starts (a timer, an effect)
    ///     belongs to nothing and lives until disposed: app-wide work started from inside a page.
    /// </summary>
    public static T Unowned<T>(Func<T> build)
    {
        var previous = Scope.Current;
        Scope.Current = null;
        try
        {
            return build();
        }
        finally
        {
            Scope.Current = previous;
        }
    }

    /// <inheritdoc cref="Unowned{T}" />
    public static void Unowned(Action build) => Unowned(() =>
    {
        build();
        return 0;
    });

    public static T Untrack<T>(Func<T> read)
    {
        var previous = Observer;
        Observer = null;
        try
        {
            return read();
        }
        finally
        {
            Observer = previous;
        }
    }

    public static void Untrack(Action read) => Untrack(() =>
    {
        read();
        return 0;
    });

    internal static void Enqueue(EffectHandle effect) => Pending.Enqueue(effect);

    /// <summary>Reruns every effect whose sources changed. <see cref="Frame.Pump" /> calls it once per frame.</summary>
    public static void Flush()
    {
        if (_flushing) return;
        _flushing = true;
        try
        {
            // ponytail: a pass limit rather than cycle detection. An effect that keeps writing
            // what it reads waits for the next frame's flush instead of hanging this one.
            for (var pass = 0; pass < 5000 && Pending.TryDequeue(out var effect); pass++) effect.RunIfChanged();
        }
        finally
        {
            _flushing = false;
        }
    }
}
