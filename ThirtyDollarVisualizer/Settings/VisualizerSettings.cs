namespace ThirtyDollarVisualizer.Settings;

public class VisualizerSettings(Action modifiedCallback)
{
    public int EventSize
    {
        get;
        set => SetAndCallModified(out field, value);
    } = 64;

    public int EventMargin
    {
        get;
        set => SetAndCallModified(out field, value);
    } = 12;

    public int LineAmount
    {
        get;
        set => SetAndCallModified(out field, value);
    } = 16;

    public string Greeting
    {
        get;
        set => SetAndCallModified(out field, value);
    } = "DON'T LECTURE ME WITH YOUR THIRTY DOLLAR VISUALIZER";

    public string? AudioBackend
    {
        get;
        set => SetAndCallModified(out field, value);
    }

    public bool TransparentFramebuffer
    {
        get;
        set => SetAndCallModified(out field, value);
    }

    public bool AutomaticScaling
    {
        get;
        set => SetAndCallModified(out field, value);
    } = true;

    public float ScrollSpeed
    {
        get;
        set => SetAndCallModified(out field, value);
    } = 7.5f;

    public string Mode
    {
        get;
        set => SetAndCallModified(out field, value);
    } = "Visualizer";

    public bool UseVsync
    {
        get;
        set => SetAndCallModified(out field, value);
    } = true;


    private void SetAndCallModified<T>(out T obj, T value)
    {
        obj = value;
        modifiedCallback.Invoke();
    }
}