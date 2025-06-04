using ThirtyDollarVisualizer.Audio.Null;

namespace ThirtyDollarVisualizer.Audio;

public readonly struct BufferHolder(Dictionary<string, Dictionary<double, AudibleBuffer>> processedBuffers)
{
    public readonly Dictionary<string, Dictionary<double, AudibleBuffer>> ProcessedBuffers = processedBuffers;

    public BufferHolder() : this(new Dictionary<string, Dictionary<double, AudibleBuffer>>())
    {
    }

    public bool TryGetBuffer(string eventName, double eventValue, out AudibleBuffer buffer)
    {
        var event_name_span = eventName.AsSpan();
        return TryGetBuffer(event_name_span, eventValue, out buffer);
    }

    public bool TryGetBuffer(ReadOnlySpan<char> eventName, double eventValue, out AudibleBuffer buffer)
    {
        var alternative_lookup = ProcessedBuffers.GetAlternateLookup<ReadOnlySpan<char>>();
        buffer = NullAudibleBuffer.EmptyBuffer;
        if (!alternative_lookup.TryGetValue(eventName, out var alternative_buffer)) return false;

        var success = alternative_buffer.TryGetValue(eventValue, out var processed_buffer);
        buffer = processed_buffer ?? NullAudibleBuffer.EmptyBuffer;
        return success;
    }
}