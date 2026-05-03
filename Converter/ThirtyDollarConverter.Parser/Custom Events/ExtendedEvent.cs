namespace ThirtyDollarParser.Custom_Events;

public class ExtendedEvent : NormalEvent, ICustomAudibleEvent
{
    public ExtendedEvent()
    {
    }

    public ExtendedEvent(BaseEvent baseEvent)
    {
        SoundEvent = baseEvent.SoundEvent;
        Value = baseEvent.Value;
        PlayTimes = baseEvent.PlayTimes;
        Volume = baseEvent.Volume;
        ValueScale = baseEvent.ValueScale;
    }

    public bool IsStandardImplementation { get; set; }

    /// <summary>
    ///     Factor that shows in which direction the audio is panned. -1 - left, 0 - centered, 1 - right and values in between
    ///     mix between them.
    /// </summary>
    public float Pan { get; set; }

    /// <summary>
    ///     The offset of the start of the sound measured in seconds.
    /// </summary>
    public double OffsetInSeconds { get; set; }

    /// <summary>
    ///     How the pan is represented visually.
    /// </summary>
    public float TDWPan => Pan * 10;

    public override string Stringify()
    {
        if (Pan != 0 && OffsetInSeconds != 0)
            return base.Stringify() + $"^{Pan:0.##}" + $">{OffsetInSeconds}";
        
        if (Pan != 0)
            return base.Stringify() + $"^{Pan:0.##}";
        
        if (OffsetInSeconds != 0)
            return base.Stringify() + $">{OffsetInSeconds}";
        
        return base.Stringify();
    }

    public override ExtendedEvent Copy()
    {
        return new ExtendedEvent
        {
            SoundEvent = SoundEvent,
            Value = Value,
            PlayTimes = PlayTimes,
            Volume = Volume,
            ValueScale = ValueScale,
            Pan = Pan,
            OffsetInSeconds = OffsetInSeconds,
            IsStandardImplementation = IsStandardImplementation,
            WorkingValue = WorkingValue,
            WorkingVolume = WorkingVolume
        };
    }
}