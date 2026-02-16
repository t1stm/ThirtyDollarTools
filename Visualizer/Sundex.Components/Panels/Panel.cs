using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Scroll;
using Sundex.Engine.Renderer.Abstract.Extensions;

namespace Sundex.Components.Panels;

public class Panel : UIElement, IColoredBackground
{
    private List<UIElement> _children = [];
    protected Lazy<ScrollBar> ScrollBar;

    public Panel(UIContext context) : this(context, 0, 0, 0, 0)
    {
    }

    protected Panel(UIContext context, float x, float y, float width, float height) : base(context, x, y, width, height)
    {
        ScrollBar = new Lazy<ScrollBar>(() => new ScrollBar(Context, this));
    }

    public bool Overflowing { get; protected set; }
    public bool ScrollOnOverflow { get; set; }

    public List<UIElement> Children
    {
        get => _children;
        set
        {
            _children = value;
            SetChildrenParent();
            InvalidateLayout();
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

    public Renderable? Background { get; set; }

    public override void Test(MouseState mouse, Vector2 scale)
    {
        if (!Visible) return;
        base.Test(mouse, scale);

        foreach (var child in Children)
            child.Test(mouse, scale);
    }

    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);
        Background?.Update();
        foreach (var child in Children) child.Update(uiContext);
    }

    public override void InvalidateCoordinates()
    {
        base.InvalidateCoordinates();
        foreach (var child in Children) child.InvalidateCoordinates();
    }

    protected override void DoLayout()
    {
        var x = (int)AbsoluteX;
        var y = (int)AbsoluteY;
        Viewport = (x, y, x + (int)Width, y + (int)Height);

        Background?.SetPosition((x, y, 0));
        Background?.Scale = (Width, Height, 1);

        foreach (var child in Children) child.Layout();
    }

    protected void SetChildrenParent()
    {
        foreach (var child in Children) child.Parent = this;
    }

    public virtual void AddChild(UIElement child)
    {
        child.Parent = this;
        _children.Add(child);
        InvalidateLayout();
    }

    public override void DrawTo(UIContext context)
    {
        if (!Visible) return;
        base.DrawTo(context);
        foreach (var child in _children)
            child.DrawTo(context);
    }

    protected override void DrawSelf(UIContext context)
    {
        if (Background != null && Visible)
            context.QueueRender(Background, Index);
    }
}