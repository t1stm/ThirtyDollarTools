using ThirtyDollarConverter.Parser;

namespace ThirtyDollarConverter.Objects;

public class TimedEvents
{
    public Sequence[] Sequences { get; set; } = [];
    public Placement[] Placement { get; set; } = [];
    public int TimingSampleRate { get; set; } = 48000;
}