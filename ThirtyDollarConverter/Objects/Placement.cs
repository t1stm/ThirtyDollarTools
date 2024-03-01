using ThirtyDollarParser;

namespace ThirtyDollarConverter.Objects;

public class Placement
{
    /// <summary>
    /// The placement's index, audio-wise.
    /// </summary>
    public ulong Index;
    /// <summary>
    /// The placement's index, sequence-wise. Used in the Thirty Dollar Visualizer.
    /// </summary>
    public ulong SequenceIndex;
    /// <summary>
    /// The event this placement holds.
    /// </summary>
    public BaseEvent Event = NormalEvent.Empty;
    /// <summary>
    /// Whether the event is processed by the PCM Encoder.
    /// </summary>
    public bool Audible = true;
}