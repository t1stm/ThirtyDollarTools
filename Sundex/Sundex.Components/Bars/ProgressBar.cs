using System.Reflection;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Shared.Renderer.Planes.Extensions;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Components.Panels;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;
using Sundex.Style.DSL.Abstract.Values.Keywords;

namespace Sundex.Components.Bars;

public class ProgressBar : UIElement
{
    public ProgressBar(UIContext context, Panel backgroundPanel, Panel foregroundPanel)
        : base(context)
    {
        BackgroundPanel = backgroundPanel;
        ForegroundPanel = foregroundPanel;
    }

    public ProgressBar(UIContext context, Renderable? bgPlaneBackground = null, Renderable? fgPlaneBackground = null) :
        this(context,
            new Panel(context)
            {
                Background = bgPlaneBackground,
                Width = new LiteralOrComputable(100, true),
                Height = new LiteralOrComputable(100, true)
            }, new Panel(context)
            {
                Background = fgPlaneBackground,
                Width = new LiteralOrComputable(0, true),
                Height = new LiteralOrComputable(100, true)
            })
    {
    }

    [NamedSetting("background")]
    public Panel BackgroundPanel
    {
        get;
        set
        {
            var old = field;
            UpdateSetDirty(ref field, value);
            field.Parent = this;
            UpdatePanelIndices(BackgroundPanel, ForegroundPanel);
            SwapDrawnPanel(old, value);
        }
    }

    [NamedSetting("foreground")]
    public Panel ForegroundPanel
    {
        get;
        set
        {
            var old = field;
            UpdateSetDirty(ref field, value);
            field.Parent = this;
            UpdatePanelIndices(BackgroundPanel, ForegroundPanel);
            SwapDrawnPanel(old, value);
        }
    }

    [NamedSetting("border-radius")]
    public LiteralOrComputable BorderRadius
    {
        get;
        set
        {
            UpdateSetDirty(ref field, value);
            BackgroundPanel.BorderRadius = value;
            ForegroundPanel.BorderRadius = value;
        }
    }

    [NamedSetting("progress")]
    public float Progress
    {
        get;
        set
        {
            if (Math.Abs(field - value) < 0.001f) return;
            UpdateSetDirty(ref field, value);
        }
    }

    public override string Tag => "progress";

    public override UIElement? Parent
    {
        get => base.Parent;
        set
        {
            base.Parent = value;
            BackgroundPanel.Parent = this;
            ForegroundPanel.Parent = this;
            UpdatePanelIndices(BackgroundPanel, ForegroundPanel);
        }
    }

    /// <summary>
    ///     Moves a drawn bar's queued planes over to a replacement panel. Here rather than in
    ///     <see cref="ApplyStyleValue" />: a style reload and a state restore both put a
    ///     previous panel back through the setter, and <c>HandleRenderableSwap</c> cannot see
    ///     the plane inside a panel - the replaced one stayed queued, frozen at its last width.
    /// </summary>
    private void SwapDrawnPanel(Panel? old, Panel current)
    {
        if (!Drawn || ReferenceEquals(old, current)) return;
        old?.StopRendering();
        current.DrawTo(Context);
    }

    private void UpdatePanelIndices(Panel? backgroundPanel, Panel? foregroundPanel)
    {
        var baseIndex = Index;
        backgroundPanel?.Index = baseIndex + 1;
        foregroundPanel?.Index = baseIndex + 2;
    }

    public override void StopRendering()
    {
        base.StopRendering();
        BackgroundPanel.StopRendering();
        ForegroundPanel.StopRendering();
    }

    public override void ApplyStyleSheet(StyleSheet styleSheet)
    {
        base.ApplyStyleSheet(styleSheet);
        BackgroundPanel.ApplyStyleSheet(styleSheet);
        ForegroundPanel.ApplyStyleSheet(styleSheet);
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

        BackgroundPanel.Width = new LiteralOrComputable(100, true);
        BackgroundPanel.Height = new LiteralOrComputable(100, true);
        ForegroundPanel.Width = new LiteralOrComputable(MathF.Min(Progress * 100, 100), true);
        ForegroundPanel.Height = new LiteralOrComputable(100, true);

        BackgroundPanel.BorderRadius = BorderRadius;
        ForegroundPanel.BorderRadius = BorderRadius;

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

    public override UIElement? HitTest(float x, float y)
    {
        // The panels aren't regular children, so include them explicitly.
        if (!Visible) return null;
        return BackgroundPanel.HitTest(x, y) ?? base.HitTest(x, y);
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

    public override void ApplyClip(Vector4i? clip)
    {
        BackgroundPanel.ApplyClip(clip);
        ForegroundPanel.ApplyClip(clip);
    }

    protected override void ApplyStyleValue(StyleSheet styleSheet, IStyleValue? styleValue, PropertyInfo propertyInfo)
    {
        if (styleValue is null) return;

        Panel newPanel;
        switch (styleValue)
        {
            case GradientValue gv when propertyInfo.PropertyType == typeof(Panel):
            {
                newPanel = new Panel(Context)
                {
                    Background = gv.GenerateGradientPlane(),
                    BorderRadius = BorderRadius,
                    Width = new LiteralOrComputable(100, true),
                    Height = new LiteralOrComputable(100, true),
                    Parent = this
                };

                break;
            }

            case ColorValue cv when propertyInfo.PropertyType == typeof(Panel):
            {
                newPanel = new Panel(Context)
                {
                    Background = new ColoredPlane
                    {
                        Color = cv.Vector
                    },
                    BorderRadius = BorderRadius,
                    Width = new LiteralOrComputable(100, true),
                    Height = new LiteralOrComputable(100, true),
                    Parent = this
                };

                break;
            }

            default:
            {
                base.ApplyStyleValue(styleSheet, styleValue, propertyInfo);
                return;
            }
        }

        propertyInfo.SetValue(this, newPanel);
    }
}