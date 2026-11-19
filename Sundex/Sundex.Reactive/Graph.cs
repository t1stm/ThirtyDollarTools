namespace Sundex.Reactive;

/// <summary>
///     Something a reaction can depend on: a <see cref="State{T}" />, or the readable side of a
///     <see cref="Computed{T}" />. Single-threaded: everything here runs on the UI thread.
/// </summary>
public abstract class Source
{
    internal readonly HashSet<Reaction> Observers = [];

    /// <summary>Goes up only when the value really changed, so a reader can tell "told" from "changed".</summary>
    internal int Version;

    /// <summary>Brings a derived value up to date, which is what can move its version.</summary>
    internal virtual void Refresh()
    {
    }

    protected void Track()
    {
        var current = Rx.Observer;
        if (current == null) return;
        Observers.Add(current);
        current.Sources[this] = Version;
    }

    protected void Notify()
    {
        if (Observers.Count == 0) return;
        foreach (var observer in Observers.ToArray()) observer.MarkDirty();
    }
}

/// <summary>Something that reads sources and reruns when they change: a computed or an effect.</summary>
public abstract class Reaction
{
    internal readonly Dictionary<Source, int> Sources = [];
    internal abstract void MarkDirty();

    internal void Unsubscribe()
    {
        foreach (var source in Sources.Keys) source.Observers.Remove(this);
        Sources.Clear();
    }

    /// <summary>A derived value that recomputed to the same answer changed nothing for its readers.</summary>
    protected bool AnySourceChanged()
    {
        foreach (var (source, version) in Sources)
        {
            source.Refresh();
            if (source.Version != version) return true;
        }

        return false;
    }
}

/// <summary>The read side of a reactive value: reading <see cref="Value" /> inside a reaction subscribes to it.</summary>
public interface IReadOnlyState<out T>
{
    /// <summary>The value, subscribing the reaction that reads it.</summary>
    T Value { get; }

    /// <summary>The value, without subscribing.</summary>
    T Peek { get; }
}
