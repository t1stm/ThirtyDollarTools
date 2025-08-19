namespace ThirtyDollarParser.Custom_Events;

public class PannedEvent : BaseEvent, ICustomAudibleEvent
{
    public bool IsStandardImplementation { get; set; }
    
    /// <summary>
    /// Factor that shows in which direction the audio is panned. -1 - left, 0 - centered, 1 - right and values in between
    /// mix between them.
    /// </summary>
    public float Pan { get; set; }
    
    /// <summary>
    /// How the pan is represented visually.
    /// </summary>
    public float TDWPan => Pan * 10;

    public PannedEvent()
    {
    }

    public PannedEvent(BaseEvent baseEvent)
    {
        SoundEvent = baseEvent.SoundEvent;
        Value = baseEvent.Value;
        OriginalLoop = baseEvent.OriginalLoop;
        PlayTimes = baseEvent.PlayTimes;
        Volume = baseEvent.Volume;
        ValueScale = baseEvent.ValueScale;
    }

    public override string Stringify()
    {
        if (Pan != 0)
            return base.Stringify() + $"^{Pan:0.##}";
        return base.Stringify();
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
            Pan = Pan,
            IsStandardImplementation = IsStandardImplementation
        };
    }
}