namespace ThirtyDollarVisualizer.Settings;

public class VisualizerSettings(Action modifiedCallback)
{
    private string? _audioBackend;
    private bool _automaticScaling = true;
    private int _eventMargin = 12;
    private int _eventSize = 64;
    private string _greeting = "DON'T LECTURE ME WITH YOUR THIRTY DOLLAR VISUALIZER";
    private int _lineAmount = 16;
    private string _mode = "Visualizer";
    private float _scrollSpeed = 7.5f;
    private bool _transparentFramebuffer;


    public int EventSize
    {
        get => _eventSize;
        set => SetAndCallModified(out _eventSize, value);
    }

    public int EventMargin
    {
        get => _eventMargin;
        set => SetAndCallModified(out _eventMargin, value);
    }

    public int LineAmount
    {
        get => _lineAmount;
        set => SetAndCallModified(out _lineAmount, value);
    }

    public string Greeting
    {
        get => _greeting;
        set => SetAndCallModified(out _greeting, value);
    }

    public string? AudioBackend
    {
        get => _audioBackend;
        set => SetAndCallModified(out _audioBackend, value);
    }

    public bool TransparentFramebuffer
    {
        get => _transparentFramebuffer;
        set => SetAndCallModified(out _transparentFramebuffer, value);
    }

    public bool AutomaticScaling
    {
        get => _automaticScaling;
        set => SetAndCallModified(out _automaticScaling, value);
    }

    public float ScrollSpeed
    {
        get => _scrollSpeed;
        set => SetAndCallModified(out _scrollSpeed, value);
    }

    public string Mode
    {
        get => _mode;
        set => SetAndCallModified(out _mode, value);
    }

    private void SetAndCallModified<T>(out T obj, T value)
    {
        obj = value;
        modifiedCallback.Invoke();
    }
}