using ThirtyDollarVisualizer.Audio.Null;

namespace ThirtyDollarVisualizer.Audio;

public readonly struct BufferHolder(Dictionary<(string event_name, double event_value), AudibleBuffer> processedBuffers)
{
    public readonly Dictionary<(string event_name, double event_value), AudibleBuffer> ProcessedBuffers = processedBuffers;

    public BufferHolder() : this(new Dictionary<(string event_name, double event_value), AudibleBuffer>())
    {
    }

    public AudibleBuffer GetBuffer(string event_name, double event_value)
    {
        return ProcessedBuffers[(event_name, event_value)];
    }

    public bool TryGetBuffer(string event_name, double event_value, out AudibleBuffer buffer)
    {
        var success = ProcessedBuffers.TryGetValue((event_name, event_value), out var processed_buffer);
        buffer = processed_buffer ?? NullAudibleBuffer.EmptyBuffer;
        return success;

    }
}