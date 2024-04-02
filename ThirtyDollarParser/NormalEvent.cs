namespace ThirtyDollarParser;

public class NormalEvent : BaseEvent
{
    public static readonly NormalEvent Empty = new()
    {
        ValueScale = ValueScale.None
    };

    /// <summary>
    ///     Creates an easily loggable string for this event.
    /// </summary>
    /// <returns>A log string.</returns>
    public override string ToString()
    {
        return
            $"Event: \"{SoundEvent ?? "Null event."}\", Value: {Value}{(ValueScale == ValueScale.Times ? 'x' : (char)0)}, PlayTimes: {PlayTimes}";
    }

    /// <summary>
    ///     Creates an identical copy of an event.
    /// </summary>
    /// <returns>The copy of the event.</returns>
    public override BaseEvent Copy()
    {
        return new NormalEvent
        {
            SoundEvent = SoundEvent,
            Value = Value,
            OriginalLoop = OriginalLoop,
            PlayTimes = PlayTimes,
            Volume = Volume,
            ValueScale = ValueScale
        };
    }
}

public enum ValueScale
{
    Divide,
    Times,
    Add,
    None
}