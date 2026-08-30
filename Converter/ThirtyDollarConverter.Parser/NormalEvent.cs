namespace ThirtyDollarConverter.Parser;

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
    ///     "!pulse"'s packed payload, read the way <see cref="Sequence" /> wrote it. The site's
    ///     text is "!pulse@pulses,frequency" - how many times the screen pulses, then how many
    ///     beats apart - packed as the pulse count in the high short and the frequency in the
    ///     low byte. Everything that reads one goes through here, so the two halves can't be
    ///     read the wrong way round.
    /// </summary>
    public static (int Pulses, int Frequency) UnpackPulse(double value)
    {
        var packed = (long)value;
        return ((short)(packed >> 8), (byte)packed);
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
                var (pulses, frequency) = UnpackPulse(Value);
                return $"!pulse@{pulses},{frequency}";
            }
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
            PlayTimes = PlayTimes,
            Volume = Volume,
            WorkingVolume = WorkingVolume,
            WorkingValue = WorkingValue,
            ValueScale = ValueScale
        };
    }
}