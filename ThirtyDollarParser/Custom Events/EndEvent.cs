namespace ThirtyDollarParser.Custom_Events;

public class EndEvent : BaseEvent, IHiddenEvent, ICustomActionEvent
{
    public override BaseEvent Copy()
    {
        return new EndEvent
        {
            OriginalLoop = OriginalLoop,
            PlayTimes = PlayTimes,
            SoundEvent = "#sequence_end"
        };
    }
}