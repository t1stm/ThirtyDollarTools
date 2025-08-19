using ThirtyDollarParser.Custom_Events;

namespace ThirtyDollarParser;

public abstract class BaseEvent
{
    /// <summary>
    /// Original loop value of the event. Do not modify.
    /// </summary>
    public float OriginalLoop = 1;

    /// <summary>
    /// Loop value of the event. Can be modified as long as it's restored from original loop after finishing.
    /// </summary>
    public float PlayTimes = 1;

    /// <summary>
    /// The sound name of the current event.
    /// </summary>
    public string? SoundEvent;

    /// <summary>
    /// Special boolean for single loop and jump events.
    /// </summary>
    public bool Triggered;

    /// <summary>
    /// The value of the current event. Used for many different things.
    /// </summary>
    public double Value;

    /// <summary>
    /// The scale of the value.
    /// </summary>
    public ValueScale ValueScale;

    /// <summary>
    /// The volume scale of the current sample. Null when the sample is following the default sequence volume.
    /// </summary>
    public double? Volume;

    /// <summary>
    /// The final volume of the current sample.
    /// </summary>
    public double WorkingVolume = 100;

    /// <summary>
    /// A method that gives a copy of the current event with no addresses shared between the two objects.
    /// </summary>
    /// <returns>A copy.</returns>
    public abstract BaseEvent Copy();

    public void Deconstruct(out string? eventName, out double eventValue)
    {
        eventName = SoundEvent;
        eventValue = Value;
    }

    public void Deconstruct(out string? eventName, out double eventValue, out double eventVolume)
    {
        eventName = SoundEvent;
        eventValue = Value;
        eventVolume = WorkingVolume;
    }

    public virtual string Stringify()
    {
        var sound = SoundEvent ?? throw new NullReferenceException();
        if (Value != 0)
            sound += $"@{Value:0.##}";
        
        if (ValueScale != ValueScale.None)
            sound += "@" + ValueScale switch
            {
                ValueScale.Divide => "/",
                ValueScale.Times => "x",
                ValueScale.Add => "+",
                _ => throw new ArgumentOutOfRangeException()
            };

        if (Volume != null)
            sound += $"%{Volume:0.##}";
        
        return sound;
    }
}