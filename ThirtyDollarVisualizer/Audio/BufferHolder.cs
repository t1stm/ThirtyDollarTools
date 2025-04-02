using ThirtyDollarVisualizer.Audio.Null;

namespace ThirtyDollarVisualizer.Audio;

public readonly struct BufferHolder(Dictionary<string, Dictionary<double, AudibleBuffer>> processedBuffers)
{
    public readonly Dictionary<string, Dictionary<double, AudibleBuffer>> ProcessedBuffers = processedBuffers;

    public BufferHolder() : this(new Dictionary<string, Dictionary<double, AudibleBuffer>>())
    {
    }
    
    public bool TryGetBuffer(string event_name, double event_value, out AudibleBuffer buffer)
    {
        var event_name_span = event_name.AsSpan();
        return TryGetBuffer(event_name_span, event_value, out buffer);
    }
    
    public bool TryGetBuffer(ReadOnlySpan<char> event_name, double event_value, out AudibleBuffer buffer)
    {
        var alternative_lookup = ProcessedBuffers.GetAlternateLookup<ReadOnlySpan<char>>();
        buffer = NullAudibleBuffer.EmptyBuffer;
        if (!alternative_lookup.TryGetValue(event_name, out var alternative_buffer))
        {
            return false;
        }
        
        var success = alternative_buffer.TryGetValue(event_value, out var processed_buffer);
        buffer = processed_buffer ?? NullAudibleBuffer.EmptyBuffer;
        return success;
    }
}