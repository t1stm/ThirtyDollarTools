namespace Sundex.Reactive;

/// <summary>
///     A value that reactions can watch. Writing an equal value does nothing; writing a different
///     one marks every reader dirty, and they rerun at the next <see cref="Rx.Flush" />, once.
/// </summary>
public sealed class State<T>(T value) : Source, IReadOnlyState<T>
{
    private T _value = value;

    public T Value
    {
        get
        {
            Track();
            return _value;
        }
        set
        {
            Frame.AssertUiThread();
            if (EqualityComparer<T>.Default.Equals(_value, value)) return;
            _value = value;
            Version++;
            Notify();
        }
    }

    public T Peek => _value;

    /// <summary>
    ///     Mutates a list or dictionary in place and tells its readers, which an equality-checked
    ///     setter can't: the reference stays the same.
    /// </summary>
    public void Update(Action<T> mutate)
    {
        Frame.AssertUiThread();
        mutate(_value);
        Version++;
        Notify();
    }

    public static implicit operator T(State<T> state) => state.Value;

    public override string ToString() => _value?.ToString() ?? "";
}
