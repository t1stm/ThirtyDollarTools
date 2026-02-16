using Sundex.Components.Abstractions;
using Sundex.Components.Bars;
using Sundex.Components.Color_Scheme;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using LoadingScene.Reports;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Animations;
using Shared.Renderer;
using Shared.Renderer.Planes;
using Shared.Renderer.Planes.Uniforms;
using Sundex.Core.Animations;
using Sundex.Engine.Renderer.Cameras;

namespace LoadingScene.Scenes;

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

        Background = new AnimatedPlane<Renderable>(
            new ColoredPlane
            {
                Color = new Vector4(0f, 0f, 0f, 1.0f),
                Scale = (dimensions.X, dimensions.Y, 1)
            },
            new KeyframedAnimation([
                new Keyframe { Color = new Vector4(0,0,0,1), LengthMs = 5000 },
                new Keyframe { Color = DarkScheme.BgMain, LengthMs = 5000 }
            ])
        );

        BackgroundElements = RootPlanes.GenerateRootPlanes(context);
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
    public AnimatedPlane<Renderable>[] BackgroundElements { get; }
    public AnimatedPlane<Renderable> Background { get; }

    public void Resize(int width, int height)
    {
        Background.Scale = (width, height, 1);
        RootPanel.Width = width;
        RootPanel.Height = height;
        RootPanel.Layout();

        RootPlanes.PositionGradients(BackgroundElements, width, height);
    }

    public void Update(IProgressReport progressReport, UIContext context, MouseState mouseState, Vector2 scale)
    {
        Background.Update();
        foreach (var gradient in BackgroundElements)
        {
            gradient.Update();
        }
        
        RootPanel.Update(context);
        RootPanel.Test(mouseState, scale);
        Label.SetTextContents(progressReport.Message);
        ProgressBar.Progress = (float)progressReport.Percentage;
    }

    public void Render(DollarStoreCamera camera, UIContext context)
    {
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
        Background.Render(camera);

        foreach (var element in BackgroundElements)
        {
            element.Render(camera);
        }
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        
        context.Clear();
        RootPanel.DrawTo(context);
        context.Render();
    }

    public void StartAnimations()
    {
        Background.Animation.Start();
        foreach (var animatedPlane in BackgroundElements)
        {
            animatedPlane.Animation.Start();
        }
    }
}