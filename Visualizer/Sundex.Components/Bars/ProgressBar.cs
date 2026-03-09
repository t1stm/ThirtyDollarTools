using System.Reflection;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Shared.Renderer.Planes.Extensions;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Components.Panels;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;
using Sundex.Style.DSL.Abstract.Values.Keywords;
using Sundex.Style.DSL;

namespace Sundex.Components.Bars;

public class ProgressBar : UIElement
{
    /*
        TODO, do these need to be panels? They can be replaced with Renderables,
        but in that case we lose the ability to apply multiple children to the elements.
        
        This will be useful if there's a need for multiple gradients in a progress bar (kinda overkill no?).
    */
    [NamedSetting("background")]
    public Panel BackgroundPanel
    {
        get;
        set
        {
            field = value;
            field.Parent = this;
            InvalidateLayout();
        }
    }
    
    [NamedSetting("foreground")]
    public Panel ForegroundPanel
    {
        get;
        set
        {
            field = value;
            field.Parent = this;
            InvalidateLayout();
        }
    }

    [NamedSetting("border-radius")]
    public LiteralOrComputable BorderRadius
    {
        get;
        set
        {
            field = value;
            BackgroundPanel.BorderRadius = value;
            ForegroundPanel.BorderRadius = value;
            InvalidateLayout();
        }
    }

    [NamedSetting("progress")]
    public float Progress
    {
        get;
        set
        {
            if (Math.Abs(field - value) < 0.001f) return;
            field = value;
            InvalidateLayout();
        }
    }

    public ProgressBar(UIContext context, Panel backgroundPanel, Panel foregroundPanel)
        : base(context)
    {
        BackgroundPanel = backgroundPanel;
        ForegroundPanel = foregroundPanel;
        BackgroundPanel.Parent = this;
        ForegroundPanel.Parent = this;
    }

    public ProgressBar(UIContext context, Renderable? bgPlaneBackground = null, Renderable? fgPlaneBackground = null) : this(context,
        new Panel(context)
        {
            Background = bgPlaneBackground
        }, new Panel(context)
        {
            Background = fgPlaneBackground,
        })
    {
    }

    public override void ApplyStyleSheet(StyleSheet styleSheet)
    {
        base.ApplyStyleSheet(styleSheet);
        BackgroundPanel.ApplyStyleSheet(styleSheet);
        ForegroundPanel.ApplyStyleSheet(styleSheet);
    }

    public override void ApplyStateOverride(StyleSheet styleSheet, string state)
    {
        base.ApplyStateOverride(styleSheet, state);
        BackgroundPanel.ApplyStateOverride(styleSheet, state);
        ForegroundPanel.ApplyStateOverride(styleSheet, state);
    }

    protected override void DrawSelf(UIContext context)
    {
        BackgroundPanel.DrawTo(context);
        ForegroundPanel.DrawTo(context);
    }

    protected override void DoLayout()
    {
        var x = (int)Computed.AbsoluteX;
        var y = (int)Computed.AbsoluteY;
        Viewport = (x, y, x + (int)Computed.Width, y + (int)Computed.Height);

        BackgroundPanel.Width = Computed.Width;
        BackgroundPanel.Height = Computed.Height;
        ForegroundPanel.Width = Computed.Width * Progress;
        ForegroundPanel.Height = Computed.Height;

        BackgroundPanel.Layout();
        ForegroundPanel.Layout();
    }

    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);
        BackgroundPanel.Update(uiContext);
        ForegroundPanel.Update(uiContext);
    }

    public override string Tag => "progress";

    public override void Test(MouseState mouse, Vector2 scale)
    {
        if (!Visible) return;
        base.Test(mouse, scale);
        BackgroundPanel.Test(mouse, scale);
    }

    public override void InvalidateLayout()
    {
        if (NeedsLayout) return;
        base.InvalidateLayout();
        BackgroundPanel.InvalidateLayout();
        ForegroundPanel.InvalidateLayout();
    }

    public override void InvalidateCoordinates()
    {
        base.InvalidateCoordinates();
        BackgroundPanel.InvalidateCoordinates();
        ForegroundPanel.InvalidateCoordinates();
    }

    protected override void ApplyStyleValue(IStyleValue? styleValue, PropertyInfo propertyInfo)
    {
        if (styleValue is null) return;
        
        switch (styleValue)
        {
            case GradientValue gv when propertyInfo.PropertyType == typeof(Panel):
            {
                var panel = new Panel(Context)
                {
                    Background = gv.GenerateGradientPlane()
                };
                propertyInfo.SetValue(this, panel);
                return;
            }

            case ColorValue cv when propertyInfo.PropertyType == typeof(Panel):
            {
                var panel = new Panel(Context)
                {
                    Background = new ColoredPlane
                    {
                        Color = cv.Vector
                    }
                };
                propertyInfo.SetValue(this, panel);
                return;
            }
            
            default:
            {
                base.ApplyStyleValue(styleValue, propertyInfo);
                return;
            }
        }
    }
}