using ThirtyDollarParser;

namespace ThirtyDollarConverter.Objects;

public struct TimedEvents
{
    public Sequence[] Sequences;
    public Placement[] Placement;
    public int TimingSampleRate;
}