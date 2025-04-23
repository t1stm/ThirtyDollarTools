using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.UI;

public class UIContext
{
    public required Camera Camera { get; set; }
    public float ViewportWidth => Camera.Width;
    public float ViewportHeight => Camera.Height;

    public Action<CursorType> RequestCursor { get; set; } = _ => { };
}

public enum CursorType
{
    Normal,
    Pointer
}