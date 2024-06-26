namespace ThirtyDollarParser.Custom_Events;

public class PannedEvent : BaseEvent, ICustomAudibleEvent
{
    /// <summary>
    ///     Factor that shows in which direction the audio is panned. -1 - left, 0 - centered, 1 - right and values in between
    ///     mix between them.
    /// </summary>
    public float Pan;

    public PannedEvent()
    {
    }

    public PannedEvent(BaseEvent base_event)
    {
        SoundEvent = base_event.SoundEvent;
        Value = base_event.Value;
        OriginalLoop = base_event.OriginalLoop;
        PlayTimes = base_event.PlayTimes;
        Volume = base_event.Volume;
        ValueScale = base_event.ValueScale;
    }

    public override PannedEvent Copy()
    {
        return new PannedEvent
        {
            SoundEvent = SoundEvent,
            Value = Value,
            OriginalLoop = OriginalLoop,
            PlayTimes = PlayTimes,
            Volume = Volume,
            ValueScale = ValueScale,
            Pan = Pan
        };
    }
}