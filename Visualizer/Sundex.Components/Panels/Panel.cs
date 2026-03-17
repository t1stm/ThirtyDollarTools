using System.Reflection;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Shared.Renderer.Planes.Extensions;
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

public class Panel(UIContext context) : UIElement(context), IColoredBackground
{
    private List<UIElement> _children = [];

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
        ApplyAnimations(Background);
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
        child.Parent = this;
        _children.Add(child);
        InvalidateLayout();
    }

    public override void DrawTo(UIContext context)
    {
        if (!Visible) return;
        base.DrawTo(context);
        Background?.Update();
        foreach (var child in _children)
            child.DrawTo(context);
    }

    protected override void DrawSelf(UIContext context)
    {
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