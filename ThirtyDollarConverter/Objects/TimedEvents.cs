using ThirtyDollarParser;

namespace ThirtyDollarConverter.Objects;

public struct TimedEvents
{
    public Sequence Sequence;
    public Placement[] Placement;
    public int TimingSampleRate;
}