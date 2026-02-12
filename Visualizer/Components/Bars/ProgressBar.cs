using Components.Abstractions;
using Components.Panels;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer;

namespace Components.Bars;

public class ProgressBar : UIElement
{
    public Panel BackgroundPanel { get; }
    public Panel ForegroundPanel { get; }

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
        : base(context, 0, 0, 0, 0)
    {
        BackgroundPanel = backgroundPanel;
        ForegroundPanel = foregroundPanel;
        BackgroundPanel.Parent = this;
        ForegroundPanel.Parent = this;
    }

    public ProgressBar(UIContext context, Renderable? bgPlaneBackground = null, Renderable? fgPlaneBackground = null) : this(context,
        new Panel(context)
        {
            Background = bgPlaneBackground,
            AutoWidth = true,
            AutoHeight = true
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
        var x = (int)AbsoluteX;
        var y = (int)AbsoluteY;
        Viewport = (x, y, x + (int)Width, y + (int)Height);

        BackgroundPanel.Width = Width;
        BackgroundPanel.Height = Height;
        ForegroundPanel.Width = Width * Progress;
        ForegroundPanel.Height = Height;

        BackgroundPanel.Layout();
        ForegroundPanel.Layout();
    }

    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);
        BackgroundPanel.Update(uiContext);
        ForegroundPanel.Update(uiContext);
    }

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
        BackgroundPanel?.InvalidateLayout();
        ForegroundPanel?.InvalidateLayout();
    }

    public override void InvalidateCoordinates()
    {
        base.InvalidateCoordinates();
        BackgroundPanel?.InvalidateCoordinates();
        ForegroundPanel?.InvalidateCoordinates();
    }
}