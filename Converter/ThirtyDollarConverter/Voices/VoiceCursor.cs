using System;
using System.Buffers;

namespace ThirtyDollarConverter.Voices;

/// <summary>
///     Streams a <see cref="VoiceList" /> a block at a time, keeping the set of voices still
///     sounding between blocks. Made for an audio feeder thread: after the first blocks against
///     the largest list it sees, <see cref="Render" />, <see cref="Seek" /> and
///     <see cref="Attach" /> allocate nothing.
/// </summary>
public sealed class VoiceCursor : IDisposable
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
        left.Clear();
        right.Clear();

        var from = Position;
        var to = from + left.Length;
        Position = to;

        var list = List;
        if (list == null) return;

        var starts = list.Starts;
        var voices = list.Voices;
        while (_next < starts.Length && starts[_next] < to) _active[_activeCount++] = _next++;

        var kept = 0;
        for (var a = 0; a < _activeCount; a++)
        {
            var index = _active[a];
            ref readonly var voice = ref voices[index];
            if (voice.End <= from) continue;

            list.MixRange(index, Math.Max(from, voice.Start), Math.Min(to, voice.End), from, left, right);
            if (voice.End > to) _active[kept++] = index;
        }

        _activeCount = kept;
    }
}
