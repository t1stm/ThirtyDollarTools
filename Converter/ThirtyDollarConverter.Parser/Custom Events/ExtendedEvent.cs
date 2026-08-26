namespace ThirtyDollarConverter.Parser.Custom_Events;

public class ExtendedEvent : NormalEvent, ICustomAudibleEvent
{
    public ExtendedEvent()
    {
    }

    /// <summary>
    ///     Widens an event so pan/offset can be written onto it - see
    ///     <c>Sequence.ProcessDefines</c>, which does that to every event of a "#define".
    ///     <see cref="BaseEvent.WorkingValue" /> comes along: it is what "!stop" counts down
    ///     and "!loopmany" spends, so leaving it at 0 made both silently do nothing inside a
    ///     define.
    /// </summary>
    public ExtendedEvent(BaseEvent baseEvent)
    {
        SoundEvent = baseEvent.SoundEvent;
        Value = baseEvent.Value;
        WorkingValue = baseEvent.WorkingValue;
        PlayTimes = baseEvent.PlayTimes;
        Volume = baseEvent.Volume;
        ValueScale = baseEvent.ValueScale;
    }

    /// <summary>
    ///     Factor that shows in which direction the audio is panned. -100 - left, 0 - centered, 100 - right and values in
    ///     between
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
    public float TDWPan => Pan / 10;

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
            Pan = Pan,
            OffsetInSeconds = OffsetInSeconds,

            SoundEvent = SoundEvent,
            Value = Value,
            PlayTimes = PlayTimes,
            Volume = Volume,
            ValueScale = ValueScale,
            WorkingValue = WorkingValue,
            WorkingVolume = WorkingVolume
        };
    }
}