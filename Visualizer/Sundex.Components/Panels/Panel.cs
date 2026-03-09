using System.Reflection;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Shared.Renderer.Planes.Extensions;
using Shared.Renderer.Planes.Uniforms;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Components.Scroll;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;
using Sundex.Style.DSL.Abstract.Values.Keywords;

namespace Sundex.Components.Panels;

public class Panel : UIElement, IColoredBackground
{
    private List<UIElement> _children = [];
    protected Lazy<ScrollBar> ScrollBar;

    public Panel(UIContext context) : base(context)
    {
        ScrollBar = new Lazy<ScrollBar>(() => new ScrollBar(Context, this));
    }

    public bool Overflowing { get; protected set; }
    public bool ScrollOnOverflow { get; set; }
    
    [NamedSetting("border-radius")]
    public LiteralOrComputable BorderRadius
    {
        get;
        set
        {
            field = value;
            InvalidateLayout();
        }
    } = 0;

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

    public override string Tag => "panel";

    public override UIElement? Parent
    {
        get => base.Parent;
        set
        {
            base.Parent = value;
            SetChildrenParent();
        }
    }

    [NamedSetting("background")]
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
        var x = (int)Computed.AbsoluteX;
        var y = (int)Computed.AbsoluteY;
        Viewport = (x, y, x + (int)Computed.Width, y + (int)Computed.Height);

        if (Background is IBorderRadius br)
            br.BorderRadius = BorderRadius.Resolve(0);

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

    protected override void ApplyStyleValue(IStyleValue? styleValue, PropertyInfo propertyInfo)
    {
        if (styleValue is null) return;

        switch (styleValue)
        {
            case GradientValue gv when propertyInfo.PropertyType == typeof(Renderable):
            {
                var gradient = gv.GenerateGradientPlane();
                if (gradient is IBorderRadius br)
                    br.BorderRadius = BorderRadius.Resolve(0);
                propertyInfo.SetValue(this, gradient);
                return;
            }

            case ColorValue cv when propertyInfo.PropertyType == typeof(Renderable):
            {
                propertyInfo.SetValue(this, new ColoredPlane
                {
                    Color = cv.Vector,
                    BorderRadius = BorderRadius.Resolve(0)
                });
                return;
            }
            
            default:
            {
                base.ApplyStyleValue(styleValue, propertyInfo);
                return;
            }
        }
    }

    public override void ApplyStyleSheet(StyleSheet styleSheet)
    {
        base.ApplyStyleSheet(styleSheet);
        foreach (var child in Children)
        {
            child.ApplyStyleSheet(styleSheet);
        }
    }

    public override void ApplyStateOverride(StyleSheet styleSheet, string state)
    {
        base.ApplyStateOverride(styleSheet, state);
        foreach (var child in Children)
            child.ApplyStateOverride(styleSheet, state);
    }
}