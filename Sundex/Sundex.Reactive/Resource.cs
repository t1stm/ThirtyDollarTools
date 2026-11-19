namespace Sundex.Reactive;

/// <summary>
///     An async value. The load runs inside an effect, so whatever it reads before its first
///     <c>await</c> is tracked: change it and the load runs again, cancelling the one in flight.
///     Made with <see cref="Rx.Load{T}" />.
/// </summary>
public sealed class Resource<T> : IReadOnlyState<T>
{
    private readonly State<T> _value;

    internal Resource(Func<CancellationToken, Task<T>> load, T initial)
    {
        _value = new State<T>(initial);
        Effect = Rx.Effect(() =>
        {
            var cancel = new CancellationTokenSource();
            Rx.OnCleanup(cancel.Cancel);

            Task<T> task;
            try
            {
                task = load(cancel.Token);
            }
            catch (Exception exception)
            {
                Rx.Untrack(() => Finish(default, exception));
                return;
            }

            Rx.Untrack(() => Loading.Value = true);
            _ = Complete(task, cancel.Token);
        });
    }

    /// <summary>True from the start of a load until it lands or fails.</summary>
    public State<bool> Loading { get; } = new(true);

    /// <summary>The last load's failure, cleared by the next success.</summary>
    public State<Exception?> Error { get; } = new(null);

    /// <summary>The effect running the load. Disposing it stops reloading.</summary>
    public EffectHandle Effect { get; }

    public T Value => _value.Value;
    public T Peek => _value.Peek;

    private async Task Complete(Task<T> task, CancellationToken token)
    {
        try
        {
            var value = await task;
            if (!token.IsCancellationRequested) Finish(value, null);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested) Finish(default, exception);
        }
    }

    private void Finish(T? value, Exception? error)
    {
        if (error == null) _value.Value = value!;
        Error.Value = error;
        Loading.Value = false;
    }

    public static implicit operator T(Resource<T> resource) => resource.Value;
}
