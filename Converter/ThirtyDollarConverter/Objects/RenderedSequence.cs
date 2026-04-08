using ThirtyDollarEncoder.PCM;
using ThirtyDollarParser;

namespace ThirtyDollarConverter.Objects;

public class RenderedSequence
{
    public required TimedEvents TimedEvents { get; set; }
    public required AudioData<float> Audio { get; set; }
    public required uint AudioSampleRate { get; set; }

    public Sequence[] Sequences => TimedEvents.Sequences;
    public Placement[] Placement => TimedEvents.Placement;
}