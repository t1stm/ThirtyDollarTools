using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarVisualizer.Objects;

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
    public float X = x;
    public float Y = y;
    public float Width = width;
    public float Height = height;

    private List<UIElement> _children = [];
    public bool AutoWidth = false, AutoHeight = false;
    public bool Visible { get; set; } = true;
    public bool IsHovered { get; set; }
    public bool IsPressed { get; set; }
    public bool UpdateCursorOnHover { get; set; }
    public UIElement? Parent { get; set; }

    public List<UIElement> Children
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

        var absX = GetAbsoluteX();
        var absY = GetAbsoluteY();

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
        Children.Add(child);
    }

    public virtual void Draw(UIContext context)
    {
        if (!Visible) return;

        DrawSelf(context.Camera);

        foreach (var child in Children)
            child.Draw(context);
    }

    protected abstract void DrawSelf(Camera camera);

    protected float GetAbsoluteX() => Parent?.GetAbsoluteX() + X ?? X;
    protected float GetAbsoluteY() => Parent?.GetAbsoluteY() + Y ?? Y;
}