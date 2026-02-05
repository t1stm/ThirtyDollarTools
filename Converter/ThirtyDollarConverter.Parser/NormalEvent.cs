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

    public override string Stringify()
    {
        switch (SoundEvent)
        {
            case "!bg":
            {
                var parsed_value = (long)Value;

                var r = (byte)parsed_value;
                var g = (byte)(parsed_value >> 8);
                var b = (byte)(parsed_value >> 16);
                var a = (byte)(parsed_value >> 24);

                var hex_string = $"{r:X2}{g:X2}{b:X2}{a:X2}";

                var seconds = (parsed_value >> 32) / 1000f;
                return $"!bg@#{hex_string},{seconds}";
            }
            case "!pulse":
            {
                var parsed_value = (long)Value;
                var repeats = (byte)parsed_value;
                float frequency = (short)(parsed_value >> 8);

                var computed_frequency = frequency * 1000f / 5f;
                return $"!pulse@{repeats},{computed_frequency}";
            }
            case "!divider":
                return "!divider\n";
            default:
                return base.Stringify();
        }
    }

    /// <summary>
    ///     Creates an identical copy of an event.
    /// </summary>
    /// <returns>The copy of the event.</returns>
    public override BaseEvent Copy()
    {
        return new NormalEvent
        {
            SoundEvent = SoundEvent is null ? null : string.Intern(SoundEvent),
            Value = Value,
            OriginalLoop = OriginalLoop,
            PlayTimes = PlayTimes,
            Volume = Volume,
            WorkingVolume = WorkingVolume,
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