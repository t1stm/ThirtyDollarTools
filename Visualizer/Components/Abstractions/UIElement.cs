using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Components.Abstractions;

public enum LayoutDirection
{
    Horizontal,
    Vertical
}

public enum Align
{
    Start,
    Center,
    End,
    Stretch
}

public abstract class UIElement(UIContext context, float x, float y, float width, float height)
{
    public UIContext Context => context;
    public bool AutoWidth = false, AutoHeight = false;
    public virtual float X { get; set; } = x;
    public virtual float Y { get; set; } = y;
    public virtual float AbsoluteX => Parent?.AbsoluteX + X ?? X;
    public virtual float AbsoluteY => Parent?.AbsoluteY + Y ?? Y;
    protected virtual int Index { get; set; }

    public virtual float Width { get; set; } = width;
    public virtual float Height { get; set; } = height;
    public bool Visible { get; set; } = true;
    public bool IsHovered { get; set; }
    public bool IsPressed { get; set; }
    public bool UpdateCursorOnHover { get; set; }

    public virtual UIElement? Parent
    {
        get;
        set
        {
            field = value;
            Index = field?.Index + 1 ?? 0;
        }
    }

    public virtual Vector4i? Viewport
    {
        get => field ?? Parent?.Viewport;
        set;
    }

    public Action<UIElement>? OnClick { get; set; }

    public virtual void Test(MouseState mouse)
    {
        if (!Visible) return;

        var absX = AbsoluteX;
        var absY = AbsoluteY;

        IsHovered = mouse.X >= absX && mouse.X <= absX + Width &&
                    mouse.Y >= absY && mouse.Y <= absY + Height;

        IsPressed = false;

        if (IsHovered && mouse.IsButtonPressed(MouseButton.Left)) OnClick?.Invoke(this);

        if (IsHovered && mouse.IsButtonDown(MouseButton.Left)) IsPressed = true;
    }

    public virtual void Update(UIContext uiContext)
    {
        if (IsHovered && UpdateCursorOnHover)
            uiContext.RequestCursor(CursorType.Pointer);
    }

    public virtual void Layout()
    {
        // overriden by inheritors
    }

    public virtual void Draw(UIContext uiContext)
    {
        if (!Visible) return;
        DrawSelf(uiContext);
    }

    protected abstract void DrawSelf(UIContext context);
}