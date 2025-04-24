using OpenTK.Windowing.GraphicsLibraryFramework;

namespace ThirtyDollarVisualizer.UI;

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

public abstract class UIElement(float x, float y, float width, float height)
{
    private List<UIElement> _children = [];
    private UIElement? _parent;
    public bool AutoWidth = false, AutoHeight = false;
    public virtual float X { get; set; } = x;
    public virtual float Y { get; set; } = y;
    protected virtual float AbsoluteX => Parent?.AbsoluteX + X ?? X;
    protected virtual float AbsoluteY => Parent?.AbsoluteY + Y ?? Y;
    protected virtual int Index { get; set; }

    public virtual float Width { get; set; } = width;
    public virtual float Height { get; set; } = height;
    public bool Visible { get; set; } = true;
    public bool IsHovered { get; set; }
    public bool IsPressed { get; set; }
    public bool UpdateCursorOnHover { get; set; }

    public UIElement? Parent
    {
        get => _parent;
        set
        {
            _parent = value;
            Index = _parent?.Index + 1 ?? 0;
            SetChildrenParent();
        }
    }

    public virtual List<UIElement> Children
    {
        get => _children;
        set
        {
            _children = value;
            SetChildrenParent();
            Layout();
        }
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

        if (IsHovered && mouse.IsButtonPressed(MouseButton.Left))
        {
            OnClick?.Invoke(this);
        }

        if (IsHovered && mouse.IsButtonDown(MouseButton.Left))
        {
            IsPressed = true;
        }

        foreach (var child in Children)
            child.Test(mouse);
    }

    public virtual void Update(UIContext context)
    {
        if (IsHovered && UpdateCursorOnHover)
            context.RequestCursor(CursorType.Pointer);

        foreach (var child in Children)
        {
            child.Update(context);
        }
    }

    public virtual void Layout()
    {
        foreach (var child in Children)
        {
            child.Layout();
        }
    }

    protected void SetChildrenParent()
    {
        foreach (var child in Children)
        {
            child.Parent = this;
        }
    }

    public virtual void AddChild(UIElement child)
    {
        child.Parent = this;
        _children.Add(child);
    }

    public virtual void Draw(UIContext context)
    {
        if (!Visible) return;

        DrawSelf(context);

        foreach (var child in _children)
            child.Draw(context);
    }

    protected abstract void DrawSelf(UIContext context);
}