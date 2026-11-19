namespace Sundex.Reactive;

/// <summary>
///     A value derived from others. Lazy: it recomputes only when read after something it read
///     changed, and tells its own readers only when the result differs from the last one.
///     <para>
///         Made with <see cref="Rx.Computed{T}" /> it belongs to the current <see cref="Scope" />
///         and lets go of its sources when that ends. Made with <c>new</c> it lives as long as what
///         it reads: right for app-wide state, a leak for anything on a page.
///     </para>
/// </summary>
public sealed class Computed<T> : Reaction, IReadOnlyState<T>
{
    private readonly Func<T> _compute;
    private readonly Node _node;
    private bool _dirty = true;
    private bool _computed;
    private bool _released;
    private T _value = default!;

    public Computed(Func<T> compute)
    {
        _compute = compute;
        _node = new Node(this);
    }

    public T Value
    {
        get
        {
            Validate();
            _node.Read();
            return _value;
        }
    }

    public T Peek => Rx.Untrack(() => Value);

    /// <summary>
    ///     Lets go of every source. Subscribed to a long-lived state, a computed would otherwise be
    ///     held by it for good, and through its closure so would whatever built it.
    /// </summary>
    public void Release()
    {
        _released = true;
        _dirty = true;
        Unsubscribe();
    }

    private void Validate()
    {
        if (_released)
        {
            // An await that outlived its owner can still read this: an answer, and no subscription.
            _value = Rx.Untrack(_compute);
            return;
        }

        if (!_dirty) return;
        if (_computed && !AnySourceChanged())
        {
            _dirty = false;
            return;
        }

        Unsubscribe();
        var previous = Rx.Observer;
        Rx.Observer = this;
        T value;
        try
        {
            value = _compute();
        }
        finally
        {
            Rx.Observer = previous;
        }

        if (!_computed || !EqualityComparer<T>.Default.Equals(_value, value)) _node.Version++;
        _value = value;
        _computed = true;
        _dirty = false;
    }

    internal override void MarkDirty()
    {
        if (_dirty) return;
        _dirty = true;
        _node.Changed();
    }

    public static implicit operator T(Computed<T> computed) => computed.Value;

    public override string ToString() => Peek?.ToString() ?? "";

    private sealed class Node(Computed<T> owner) : Source
    {
        internal override void Refresh() => owner.Validate();
        public void Read() => Track();
        public void Changed() => Notify();
    }
}
