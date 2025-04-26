using OpenTK.Windowing.GraphicsLibraryFramework;

namespace ThirtyDollarVisualizer.UI;

public class Panel(float x, float y, float width, float height) : UIElement(x, y, width, height)
{
    private List<UIElement> _children = [];
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
    
    public override UIElement? Parent
    {
        get => base.Parent;
        set
        {
            base.Parent = value;
            SetChildrenParent();
        }
    }
    
    public override void Test(MouseState mouse)
    {
        if (!Visible) return;
        base.Test(mouse);

        foreach (var child in Children)
            child.Test(mouse);
    }
    
    public override void Update(UIContext context)
    {
        base.Update(context);
        foreach (var child in Children)
        {
            child.Update(context);
        }
    }

    public override void Layout()
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

    public override void Draw(UIContext context)
    {
        if (!Visible) return;
        base.Draw(context);
        foreach (var child in _children)
            child.Draw(context);
    }

    protected override void DrawSelf(UIContext context)
    {
        // draws children
    }
}