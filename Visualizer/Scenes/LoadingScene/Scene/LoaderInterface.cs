using Components.Abstractions;
using Components.Bars;
using Components.Color_Scheme;
using Components.Labels;
using Components.Panels;
using LoadingScene.Reports;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Renderer.Planes;
using Shared.Renderer.Planes.Uniforms;
using ThirtyDollarVisualizer.Engine.Renderer.Cameras;

namespace LoadingScene.Scene;

public class LoaderInterface
{
    public LoaderInterface(UIContext context, Camera camera, Action action)
    {
        var dimensions = camera.Viewport;
        RootPanel = new FlexPanel(context)
        {
            X = 0,
            Y = 0,
            Width = dimensions.X,
            Height = dimensions.Y,
            HorizontalAlign = Align.Center,
            VerticalAlign = Align.Center,
            Direction = LayoutDirection.Vertical,
            Spacing = 25
        };

        RootPanel.Children =
        [
            Label = new Label(context, "Loading...")
            {
                FontSizePx = 48
            },
            ProgressBar = GenerateProgressBar(context),
            new Button(context, new Label(context, "Start Loading")
            {
                Color = new Vector4(0, 0, 0, 1),
            })
            {
                OnClick = _ => action(),
                Padding = 8
            },
        ];

        RootGradients = RootPlanes.GenerateRootPlanes(context);
    }

    private static ProgressBar GenerateProgressBar(UIContext context)
    {
        var backgroundGradient = new ColoredPlane
        {
            Color = new Vector4(0.25f, 0.25f, 0.25f, 0.5f),
            BorderRadius = 10f
        };

        var foregroundGradient = new GradientPlane
        {
            GradientType = GradientType.Linear,
            GradientColors = [DarkScheme.AccentBlue, DarkScheme.BlueDark],
            GradientStops = [0.0f, 1.0f],
            BorderRadius = 10f
        };

        return new ProgressBar(context, backgroundGradient, foregroundGradient)
        {
            Width = 400f,
            Height = 30f
        };
    }

    public Panel RootPanel { get; }
    public ProgressBar ProgressBar { get; set; }
    public Label Label { get; set; }
    public AnimatedPlane<GradientPlane>[] RootGradients { get; set; }

    public void Resize(int width, int height)
    {
        RootPanel.Width = width;
        RootPanel.Height = height;
        RootPanel.Layout();

        RootPlanes.PositionGradients(RootGradients, width, height);
    }

    public void Update(IProgressReport progressReport, MouseState mouseState, Vector2 scale)
    {
        foreach (var gradient in RootGradients)
        {
            gradient.Update();
        }

        RootPanel.Test(mouseState, scale);
        Label.SetTextContents(progressReport.Message);
        ProgressBar.Progress = (float)progressReport.Percentage;
    }

    public void Render(DollarStoreCamera camera, UIContext context)
    {
        context.Clear();
        RootPanel.DrawTo(context);

        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);

        foreach (var gradient in RootGradients)
        {
            gradient.Render(camera);
        }

        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        context.Render();
    }
}