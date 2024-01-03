using ThirtyDollarVisualizer.Audio.Null;

namespace ThirtyDollarVisualizer.Audio;

public readonly struct BufferHolder(Dictionary<string, Dictionary<double, AudibleBuffer>> processedBuffers)
{
    public readonly Dictionary<string, Dictionary<double, AudibleBuffer>> ProcessedBuffers = processedBuffers;

    public AudibleBuffer GetBuffer(string event_name, double event_value)
    {
        return ProcessedBuffers[event_name][event_value];
    }

    public bool TryGetBuffer(string event_name, double event_value, out AudibleBuffer buffer)
    {
        AudibleBuffer? temp_buffer = null;
        var success = ProcessedBuffers.TryGetValue(event_name, out var processed_events) && 
                      processed_events.TryGetValue(event_value, out temp_buffer);

        buffer = temp_buffer ?? NullAudibleBuffer.EmptyBuffer;
        return success;

    }
}