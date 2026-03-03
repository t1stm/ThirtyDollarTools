using System.Reflection;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;
using Sundex.Components.Panels;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;
using Sundex.Style.DSL.Abstract.Values.Keywords;

namespace Sundex.Components.Bars;

public class ProgressBar : UIElement
{
    /*
        TODO, do these need to be panels? They can be replaced with Renderables,
        but in that case we lose the ability to apply multiple children to the elements.
        
        This will be useful if there's a need for multiple gradients in a progress bar (kinda overkill no?).
    */     
    [NamedSetting("background")]
    public Panel BackgroundPanel { get; set; }
    
    [NamedSetting("foreground")]
    public Panel ForegroundPanel { get; set; }

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
                // TODO
                return;
            }

            case ColorValue cv when propertyInfo.PropertyType == typeof(Panel):
            {
                // TODO
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