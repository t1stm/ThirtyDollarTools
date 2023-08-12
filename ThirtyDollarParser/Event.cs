namespace ThirtyDollarParser;

public class Event
{
    public string? SoundEvent { get; init; }
    public double Value { get; set; }
    public int OriginalLoop { get; set; } = 1;
    public int PlayTimes { get; set; } = 1;
    public double? Volume { get; set; }
    public ValueScale ValueScale { get; init; }

    public override string ToString()
    {
        return
            $"Event: \"{SoundEvent ?? "Null event."}\", Value: {Value}{(ValueScale == ValueScale.Times ? 'x' : (char)0)}, PlayTimes: {PlayTimes}";
    }

    public Event Copy()
    {
        return new Event
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
    Times,
    Add,
    None
}