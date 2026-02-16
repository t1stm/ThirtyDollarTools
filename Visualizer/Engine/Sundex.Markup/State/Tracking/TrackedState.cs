namespace Sunder.Markup.State.Tracking;

public class TrackedState<T>(T initialValue)
{
    public T Value
    {
        get;
        set
        {
            field = value;
            OnChange(value);
        }
    } = initialValue;

    public Action<T> OnChange { get; set; } = _ => { };
}