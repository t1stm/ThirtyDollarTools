using System.Reflection;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Shared.Renderer.Planes.Extensions;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;
using Sundex.Style.DSL.Abstract.Values.Keywords;

namespace Sundex.Components.Panels;

public class Panel(UIContext context) : UIElement(context), IColoredBackground, IPositioningElement
{
    private List<UIElement> _children = [];

    [NamedSetting("border-radius")]
    public LiteralOrComputable BorderRadius
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = 0;

    public List<UIElement> Children
    {
        get => _children;
        set
        {
            _children = value;
            SetChildrenParent();
            InvalidateLayout();

            // Mirrors AddChild: replacing the list on an already-live panel must queue
            // the new children's renderables too, or they're hit-testable but invisible
            // until something else happens to re-run DrawTo on the tree.
            if (Drawn)
                foreach (var child in _children)
                    child.DrawTo(Context);
        }
    }

    public override string Tag => "panel";

    /// <summary>
    ///     Depth index. Cascades to children so subtrees composed while detached
    ///     (e.g. rows built before AddChild) get correct z-order/hit-test indices
    ///     once parented into a deeper tree.
    /// </summary>
    public override int Index
    {
        get => base.Index;
        internal set
        {
            base.Index = value;
            foreach (var child in Children) child.Index = value + 1;
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

    [RenderPriority(0)]
    [NamedSetting("background")]
    public Renderable? Background
    {
        get;
        set
        {
            var old = field;
            field = value;
            HandleRenderableSwap(old, value, nameof(Background));
        }
    }

    [NamedSetting("direction")]
    public virtual LayoutDirection Direction
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = LayoutDirection.Horizontal;

    [NamedSetting("padding")]
    public virtual float Padding
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = 0;

    [NamedSetting("spacing")]
    public virtual float Spacing
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = 0;

    /// <summary>
    ///     True while this panel's renderables are queued (DrawTo ran and StopRendering
    ///     hasn't). AddChild consults it, so composing a subtree while detached never
    ///     queues renders - the whole subtree queues when it is drawn into a live tree.
    /// </summary>
    protected internal bool Drawn { get; private set; }

    public override void StopRendering()
    {
        Drawn = false;
        if (Background != null)
            Context.DequeueRender(Background, Index);

        // Cascade so removing a subtree (e.g. closing a modal) dequeues everything in it.
        foreach (var child in Children) child.StopRendering();
    }

    public override void Test(MouseState mouse, Vector2 scale)
    {
        if (!Visible) return;
        base.Test(mouse, scale);

        foreach (var child in Children)
            child.Test(mouse, scale);
    }

    public override UIElement? HitTest(float x, float y)
    {
        if (!Visible) return null;
        var best = base.HitTest(x, y);
        foreach (var child in Children)
        {
            var hit = child.HitTest(x, y);
            if (hit != null && (best == null || hit.Index >= best.Index)) best = hit;
        }

        return best;
    }

    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);
        ApplyAnimations(Background);
        foreach (var child in Children) child.Update(uiContext);
    }

    public override void InvalidateCoordinates()
    {
        base.InvalidateCoordinates();
        foreach (var child in Children) child.InvalidateCoordinates();
    }

    public override void ApplyClip(Vector4i? clip)
    {
        Background?.ClipRect = clip;
        foreach (var child in Children) child.ApplyClip(clip);
    }

    protected override void DoLayout()
    {
        var x = (int)Computed.AbsoluteX;
        var y = (int)Computed.AbsoluteY;
        Viewport = (x, y, x + (int)Computed.Width, y + (int)Computed.Height);

        if (Background is IBorderRadius br)
            br.BorderRadius = BorderRadius.Resolve(Computed.Height);

        Background?.SetPosition((x, y, 0));
        Background?.Scale = (Computed.Width, Computed.Height, 1);

        foreach (var child in Children) child.Layout();
    }

    protected void SetChildrenParent()
    {
        foreach (var child in Children) child.Parent = this;
    }

    public virtual void AddChild(UIElement child)
    {
        if (child.Parent is Panel oldParent) oldParent.RemoveChild(child);
        _children.Add(child);
        // A flex-dictated size from the previous parent must not follow the child here.
        child.ParentAssignedWidth = null;
        child.ParentAssignedHeight = null;
        child.Parent = this;
        // DrawTo queues the child's renderables but lays it out against this panel's
        // possibly-stale Computed; re-invalidate so the next Layout pass repositions it.
        // A panel that isn't drawn itself must not queue its children either - they
        // queue with the whole subtree once this panel gets its DrawTo.
        if (Drawn) child.DrawTo(Context);
        child.InvalidateCoordinates();
        InvalidateLayout();
    }

    public void RemoveChild(UIElement child)
    {
        _children.Remove(child);
        Context.NotifyDetached(child);
        child.Parent = null;
        child.StopRendering();
        InvalidateLayout();
    }

    public override void DrawTo(UIContext ctx)
    {
        if (!Visible) return;
        Drawn = true;
        base.DrawTo(ctx);
        Background?.Update();
        foreach (var child in _children)
            child.DrawTo(ctx);
    }

    protected override void DrawSelf(UIContext ctx)
    {
        // Append: within a depth layer, render order follows draw order, so a later
        // sibling stacks above an earlier one - matching hit-test priority. (Insert
        // at the front would reverse sibling stacking: the first-drawn child would
        // paint on top of everything drawn after it.)
        if (Background != null)
            ctx.QueueRender(Background, Index);
    }

    protected override void ApplyStyleValue(StyleSheet styleSheet, IStyleValue? styleValue, PropertyInfo propertyInfo)
    {
        if (styleValue is null) return;

        var oldValue = propertyInfo.GetValue(this);
        Renderable? plane;
        switch (styleValue)
        {
            case GradientValue gv when propertyInfo.PropertyType == typeof(Renderable):
                {
                    var gradient = gv.GenerateGradientPlane();
                    gradient.BorderRadius = BorderRadius.Resolve(Computed.Height);

                    propertyInfo.SetValue(this, plane = gradient);
                    break;
                }

            case ColorValue cv when propertyInfo.PropertyType == typeof(Renderable):
                {
                    var colored = new ColoredPlane
                    {
                        Color = cv.Vector,
                        BorderRadius = BorderRadius.Resolve(Computed.Height)
                    };

                    propertyInfo.SetValue(this, plane = colored);
                    break;
                }

            default:
                {
                    base.ApplyStyleValue(styleSheet, styleValue, propertyInfo);
                    return;
                }
        }

        HandleRenderableSwap(oldValue, plane, propertyInfo.Name);
    }

    public override void ApplyStyleSheet(StyleSheet styleSheet)
    {
        base.ApplyStyleSheet(styleSheet);
        foreach (var child in Children) child.ApplyStyleSheet(styleSheet);
    }
}