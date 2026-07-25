namespace ThirtyDollarParser.Custom_Events;

public class LegacySequenceEvent : BaseEvent, IHiddenEvent
{
    public LegacySequenceEvent()
    {
        SoundEvent = "#legacy";
    }

    public override BaseEvent Copy()
    {
        return new LegacySequenceEvent();
    }
}