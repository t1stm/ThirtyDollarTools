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
    private float _absoluteX;
    private float _absoluteY;
    private bool _coordinatesDirty = true;

    public bool AutoWidth { get => field; set { if (field == value) return; field = value; InvalidateLayout(); } } = false;
    public bool AutoHeight { get => field; set { if (field == value) return; field = value; InvalidateLayout(); } } = false;
    public UIContext Context => context;

    public virtual float X
    {
        get;
        set
        {
            field = value;
            InvalidateCoordinates();
            InvalidateLayout();
        }
    } = x;

    public virtual float Y
    {
        get;
        set
        {
            field = value;
            InvalidateCoordinates();
            InvalidateLayout();
        }
    } = y;

    public virtual float AbsoluteX
    {
        get
        {
            if (_coordinatesDirty) UpdateAbsoluteCoordinates();
            return _absoluteX;
        }
    }

    public virtual float AbsoluteY
    {
        get
        {
            if (_coordinatesDirty) UpdateAbsoluteCoordinates();
            return _absoluteY;
        }
    }

    protected virtual int Index { get; set; }

    public virtual float Width
    {
        get;
        set
        {
            field = value;
            InvalidateLayout();
        }
    } = width;

    public virtual float Height
    {
        get;
        set
        {
            field = value;
            InvalidateLayout();
        }
    } = height;

    public bool Visible
    {
        get => field;
        set
        {
            if (field == value) return;
            field = value;
            InvalidateLayout();
        }
    } = true;
    public bool IsHovered { get; set; }
    public bool IsPressed { get; set; }
    public bool UpdateCursorOnHover { get; set; }

    public bool NeedsLayout { get; protected set; } = true;

    public virtual UIElement? Parent
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Index = field?.Index + 1 ?? 0;
            InvalidateCoordinates();
            InvalidateLayout();
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

    public virtual void InvalidateLayout()
    {
        if (NeedsLayout) return;
        NeedsLayout = true;
        Parent?.InvalidateLayout();
    }

    public virtual void InvalidateCoordinates()
    {
        _coordinatesDirty = true;
        NeedsLayout = true;
    }

    protected virtual void UpdateAbsoluteCoordinates()
    {
        _absoluteX = Parent?.AbsoluteX + X ?? X;
        _absoluteY = Parent?.AbsoluteY + Y ?? Y;
        _coordinatesDirty = false;
    }

    public virtual void Layout()
    {
        if (!NeedsLayout) return;
        DoLayout();
        NeedsLayout = false;
    }

    protected virtual void DoLayout()
    {
    }

    public virtual void DrawTo(UIContext uiContext)
    {
        if (!Visible) return;
        Layout();
        DrawSelf(uiContext);
    }

    protected abstract void DrawSelf(UIContext context);
}