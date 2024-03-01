namespace ThirtyDollarParser;

public abstract class BaseEvent
{
    /// <summary>
    /// A method that gives a copy of the current event with no addresses shared between the two objects.
    /// </summary>
    /// <returns>A copy.</returns>
    public abstract BaseEvent Copy();
    
    /// <summary>
    /// The sound name of the current event.
    /// </summary>
    public string? SoundEvent;

    /// <summary>
    /// The value of the current event. Used for many different things.
    /// </summary>
    public double Value;
    
    /// <summary>
    /// Original loop value of the event. Do not modify.
    /// </summary>
    public int OriginalLoop = 1;
    
    /// <summary>
    /// Loop value of the event. Can be modified as long as it's restored from original loop after finishing.
    /// </summary>
    public int PlayTimes = 1;

    /// <summary>
    /// The volume of the current sample. Null when the sample is following the default sequence volume.
    /// </summary>
    public double? Volume;

    /// <summary>
    /// The scale of the value.
    /// </summary>
    public ValueScale ValueScale;

    /// <summary>
    /// Special boolean for single loop and jump events.
    /// </summary>
    public bool Triggered;
    
    public void Deconstruct(out string? event_name, out double event_value)
    {
        event_name = SoundEvent;
        event_value = Value;
    }
    
    public void Deconstruct(out string? event_name, out double event_value, out double? event_volume)
    {
        event_name = SoundEvent;
        event_value = Value;
        event_volume = Volume;
    }
}