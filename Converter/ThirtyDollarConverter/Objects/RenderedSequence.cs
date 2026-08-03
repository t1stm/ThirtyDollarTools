using System.Collections.Generic;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Parser;

namespace ThirtyDollarConverter.Objects;

public class RenderedSequence
{
    public required TimedEvents TimedEvents { get; set; }
    public required AudioData<float> Audio { get; set; }
    public required uint AudioSampleRate { get; set; }
    public AudioMixer? Mixer { get; set; }
    public Dictionary<(string, double), ProcessedEvent>? ProcessedEvents { get; set; }

    public Sequence[] Sequences => TimedEvents.Sequences;
    public Placement[] Placement => TimedEvents.Placement;
}