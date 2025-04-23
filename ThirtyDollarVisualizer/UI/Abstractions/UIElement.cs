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
    public bool AutoWidth = false, AutoHeight = false;
    public bool Visible { get; set; } = true;
    public bool IsHovered { get; set; }
    public bool IsPressed { get; set; }
    public bool UpdateCursorOnHover { get; set; }

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
    }

    public virtual void Draw(UIContext context)
    {
        if (!Visible) return;


            child.Draw(context);
    }

}