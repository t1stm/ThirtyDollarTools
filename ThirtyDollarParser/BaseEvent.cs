namespace ThirtyDollarParser;

public abstract class BaseEvent
{
    /// <summary>
    ///     Original loop value of the event. Do not modify.
    /// </summary>
    public int OriginalLoop = 1;

    /// <summary>
    ///     Loop value of the event. Can be modified as long as it's restored from original loop after finishing.
    /// </summary>
    public int PlayTimes = 1;

    /// <summary>
    ///     The sound name of the current event.
    /// </summary>
    public string? SoundEvent;

    /// <summary>
    ///     Special boolean for single loop and jump events.
    /// </summary>
    public bool Triggered;

    /// <summary>
    ///     The value of the current event. Used for many different things.
    /// </summary>
    public double Value;

    /// <summary>
    ///     The scale of the value.
    /// </summary>
    public ValueScale ValueScale;

    /// <summary>
    ///     The volume of the current sample. Null when the sample is following the default sequence volume.
    /// </summary>
    public double? Volume;

    /// <summary>
    ///     A method that gives a copy of the current event with no addresses shared between the two objects.
    /// </summary>
    /// <returns>A copy.</returns>
    public abstract BaseEvent Copy();

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