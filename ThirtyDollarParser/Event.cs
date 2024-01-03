namespace ThirtyDollarParser;

public class Event
{
    /// <summary>
    /// The sound name of the current event.
    /// </summary>
    public string? SoundEvent { get; init; }
    
    /// <summary>
    /// The value of the current event. Used for many different things.
    /// </summary>
    public double Value { get; set; }
    
    /// <summary>
    /// Original loop value of the event. Do not modify.
    /// </summary>
    public int OriginalLoop { get; set; } = 1;
    
    /// <summary>
    /// Loop value of the event. Can be modified as long as it's restored from original loop after finishing.
    /// </summary>
    public int PlayTimes { get; set; } = 1;
    
    /// <summary>
    /// The volume of the current sample. Null when the sample is following the default sequence volume.
    /// </summary>
    public double? Volume { get; set; }
    
    /// <summary>
    /// The scale of the value.
    /// </summary>
    public ValueScale ValueScale { get; init; }

    /// <summary>
    /// Special boolean for single loop and jump events.
    /// </summary>
    public bool Triggered { get; set; }

    /// <summary>
    /// Creates an easily loggable string for this event.
    /// </summary>
    /// <returns>A log string.</returns>
    public override string ToString()
    {
        return
            $"Event: \"{SoundEvent ?? "Null event."}\", Value: {Value}{(ValueScale == ValueScale.Times ? 'x' : (char)0)}, PlayTimes: {PlayTimes}";
    }

    /// <summary>
    /// Creates an identical copy of an event.
    /// </summary>
    /// <returns>The copy of the event.</returns>
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
    Divide,
    Times,
    Add,
    None
}