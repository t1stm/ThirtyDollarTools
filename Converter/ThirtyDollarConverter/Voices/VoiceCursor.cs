using System;
using System.Buffers;

namespace ThirtyDollarConverter.Voices;

/// <summary>
///     Streams a <see cref="VoiceList" /> a block at a time, keeping the set of voices still
///     sounding between blocks. Made for an audio feeder thread: after the first blocks against
///     the largest list it sees, <see cref="Render" />, <see cref="Advance" />, <see cref="Seek" />
///     and <see cref="Attach" /> allocate nothing.
/// </summary>
public sealed class VoiceCursor : IVoiceCursor
{
    private int[] _active = [];
    private int _activeCount;
    private int _next;

    /// <summary>The list being read; null renders silence.</summary>
    public VoiceList? List { get; private set; }

    /// <summary>Output sample the next <see cref="Render" /> starts at.</summary>
    public int Position { get; private set; }

    public void Dispose()
    {
        if (_active.Length > 0) ArrayPool<int>.Shared.Return(_active);
        _active = [];
        List = null;
    }

    /// <summary>
    ///     Switches to <paramref name="list" /> at <paramref name="position" /> and returns the list
    ///     it was reading, which the caller now disposes - the cursor holds no reference of its own.
    /// </summary>
    public VoiceList? Attach(VoiceList? list, int position)
    {
        var previous = List;
        List = list;

        if (list != null && _active.Length < list.Count)
        {
            if (_active.Length > 0) ArrayPool<int>.Shared.Return(_active);
            _active = ArrayPool<int>.Shared.Rent(list.Count);
        }

        Seek(position);
        return previous;
    }

    /// <summary>Moves to <paramref name="position" />, picking up every voice sounding there.</summary>
    public void Seek(int position)
    {
        Position = position;
        _activeCount = 0;
        _next = 0;
        if (List == null) return;

        var starts = List.Starts;
        var voices = List.Voices;
        var i = List.LowerBound((long)position - List.Longest);
        for (; i < starts.Length && starts[i] < position; i++)
            if (voices[i].End > position)
                _active[_activeCount++] = i;

        _next = i;
    }

    /// <summary>
    ///     Mixes the next <c>left.Length</c> samples into <paramref name="left" /> and
    ///     <paramref name="right" /> (overwriting them) and advances. Both spans must be the same
    ///     length; a mono list leaves <paramref name="right" /> silent.
    /// </summary>
    public void Render(Span<float> left, Span<float> right)
    {
        var from = Position;
        var active = Advance(left.Length);
        if (List is { } list)
        {
            list.MixBlock(active, from, left, right);
            return;
        }

        left.Clear();
        right.Clear();
    }

    /// <summary>
    ///     Moves on by <paramref name="frames" /> without mixing, and returns the voices that sound
    ///     in that block in the order <see cref="Render" /> mixes them - ascending index - for
    ///     <see cref="VoiceList.MixBlock" /> or a mixer of another kind. The span is the cursor's own
    ///     and changes when it moves again.
    /// </summary>
    public ReadOnlySpan<int> Advance(int frames)
    {
        var from = Position;
        var to = from + frames;
        Position = to;

        var list = List;
        if (list == null) return [];

        var starts = list.Starts;
        var voices = list.Voices;

        // drop the voices that ended before this block, then add the ones that start in it
        var kept = 0;
        for (var a = 0; a < _activeCount; a++)
        {
            var index = _active[a];
            if (voices[index].End > from) _active[kept++] = index;
        }

        _activeCount = kept;
        while (_next < starts.Length && starts[_next] < to) _active[_activeCount++] = _next++;
        return _active.AsSpan(0, _activeCount);
    }

    bool IVoiceCursor.TryRender(Span<float> left, Span<float> right, bool force)
    {
        Render(left, right);
        return true;
    }
}
