namespace EditorScene.State;

/// <summary>
///     One editor selection: an ordered list (last = primary) with an "exactly one" derived
///     view and a single change event. The editor has four of these - tracks, placements,
///     notes and faithful items - which differed only in their element type;
///     <see cref="EditorState" />'s Select*/Set*/AddTo*/RemoveFrom* methods forward here.
///     Membership is reference identity: every element type is a plain class, so two
///     equal-looking notes stay two separately selectable notes.
/// </summary>
public sealed class Selection<T> where T : class
{
    private readonly List<T> _items = [];

    /// <summary>Everything selected, in selection order (last = primary).</summary>
    public IReadOnlyList<T> Items => _items;

    /// <summary>
    ///     Non-null only when exactly one thing is selected. The single-selection consumers
    ///     (inspector forms, highlights, cascades) read this and go quiet on a multi-selection.
    /// </summary>
    public T? Single => _items.Count == 1 ? _items[0] : null;

    public int Count => _items.Count;

    /// <summary>Fired once per change - once per batch mutation too - with <see cref="Single" />.</summary>
    public event Action<T?>? Changed;

    public bool Contains(T item)
    {
        return _items.Contains(item);
    }

    /// <summary>Replaces the selection with a single item, or clears it when null.</summary>
    public void SetOne(T? item)
    {
        Set(item != null ? [item] : []);
    }

    /// <summary>Replaces the whole selection. Silent when the result is the same list it already was.</summary>
    public void Set(IEnumerable<T> items)
    {
        var next = items as IReadOnlyList<T> ?? [.. items];
        if (_items.Count == next.Count && !_items.Where((item, i) => !ReferenceEquals(item, next[i])).Any()) return;

        _items.Clear();
        _items.AddRange(next);
        Changed?.Invoke(Single);
    }

    /// <summary>Appends items not already selected. No-op for ones already present (append, not toggle).</summary>
    public void Add(IEnumerable<T> items)
    {
        var added = false;
        foreach (var item in items)
        {
            if (_items.Contains(item)) continue;
            _items.Add(item);
            added = true;
        }

        if (added) Changed?.Invoke(Single);
    }

    /// <summary>Removes items from the selection. No-op for ones not present.</summary>
    public void Remove(IEnumerable<T> items)
    {
        if (items.Aggregate(false, (current, item) => current | _items.Remove(item)))
            Changed?.Invoke(Single);
    }

    /// <summary>Adds the item to the selection, or drops it when it is already in - a ctrl/cmd-click.</summary>
    public void Toggle(T item)
    {
        if (!_items.Remove(item)) _items.Add(item);
        Changed?.Invoke(Single);
    }

    /// <summary>Drops everything that no longer passes - what undo/redo prunes dead references with.</summary>
    public void Keep(Func<T, bool> alive)
    {
        Set([.. _items.Where(alive)]);
    }

    /// <summary>Swaps a selected item for its replacement, in place. Silent when it was not selected.</summary>
    public void Replace(T old, T replacement)
    {
        if (!_items.Contains(old)) return;
        Set([.. _items.Select(item => ReferenceEquals(item, old) ? replacement : item)]);
    }
}
