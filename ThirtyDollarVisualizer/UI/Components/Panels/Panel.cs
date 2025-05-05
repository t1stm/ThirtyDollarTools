using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.UI.Components.Scroll;

namespace ThirtyDollarVisualizer.UI;

public class Panel : UIElement, IColoredBackground
{
    public Panel() : this(0, 0, 0, 0) { }
    
    public ColoredPlane? Background { get; set; }
    public bool Overflowing { get; protected set; }
    public bool ScrollOnOverflow { get; set; }
    
    private List<UIElement> _children = [];
    protected Lazy<ScrollBar> _scrollBar;

    protected Panel(float x, float y, float width, float height) : base(x, y, width, height)
    {
        _scrollBar = new Lazy<ScrollBar>(() => new ScrollBar(this));
    }

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
        var x = (int)AbsoluteX;
        var y = (int)AbsoluteY;
        Viewport = (x, y, x + (int)Width, y + (int)Height);
        
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
        if (Background != null && Visible)
            context.QueueRender(Background, Index);
    }
}