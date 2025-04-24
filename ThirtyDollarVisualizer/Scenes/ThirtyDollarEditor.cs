using System.Diagnostics;
using System.Globalization;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.Fonts;
using ThirtyDollarVisualizer.Audio;
using ThirtyDollarVisualizer.Audio.Null;
using ThirtyDollarVisualizer.Helpers.Miscellaneous;
using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Settings;
using ThirtyDollarVisualizer.UI;

namespace ThirtyDollarVisualizer.Scenes;

public class ThirtyDollarEditor(int width, int height, VisualizerSettings settings, AudioContext? audio_context)
    : IScene
{
    private readonly UIContext UIContext = new()
    {
        Camera = new DollarStoreCamera((0, 0, 0), (width, height))
    };

    private readonly VisualizerSettings Settings = settings;
    private readonly AudioContext AudioContext = audio_context ?? new NullAudioContext();
    private Manager Parent = null!;

    private readonly int InitialWidth = width;
    private readonly int InitialHeight = height;

    private FlexPanel? Display;
    private FlexPanel? ErrorDisplay;
    private CursorType CurrentCursor;
    
    private string ErrorMessage = "This message hasn't been updated yet. The error remains hidden...";
    
    private long LastFpsUpdate;
    private double LastFpsValue;
    private Label? FpsLabel;
    private static readonly TimeSpan FpsUpdateTimeout = TimeSpan.FromMilliseconds(5);
    private readonly FpsCounter FpsCounter = new(16);

    public void Init(Manager manager)
    {
        Parent = manager;
        Display = new FlexPanel(0, 0, InitialWidth, InitialHeight)
        {
            Direction = LayoutDirection.Vertical,
            Children =
            [
                new FlexPanel(0, 0, 0, 32) // Header
                {
                    Background = new ColoredPlane((0.2f, 0.2f, 0.2f, 1f)),
                    Direction = LayoutDirection.Horizontal,
                    Spacing = 10,
                    Padding = 5,
                    VerticalAlign = Align.Center,
                    AutoWidth = true,
                    Children =
                    [
                        new Label("Thirty Dollar Editor")
                        {
                            FontStyle = FontStyle.Bold,
                            UpdateCursorOnHover = true
                        },
                        new DropDownLabel("File", [
                            new Label("New")
                            {
                                UpdateCursorOnHover = true
                            },
                            new Label("Open")
                            {
                                UpdateCursorOnHover = true
                            },
                            new Label("Save")
                            {
                                UpdateCursorOnHover = true
                            },
                            new Label("Save As")
                            {
                                UpdateCursorOnHover = true
                            },
                        ]),
                        FpsLabel = new Label("FPS: ", LabelMode.CachedDynamic)
                    ]
                },
                new FlexPanel(0, 0, 0, 0) // Main Display
                {
                    Background = new ColoredPlane((0.3f, 0.3f, 0.3f, 1f)),
                    AutoHeight = true,
                    AutoWidth = true,
                    Padding = 10,
                    Children =
                    [
                    ]
                }
            ]
        };

        UIContext.RequestCursor = cursor => { CurrentCursor = cursor; };

        Display.Layout();
    }

    public void Start()
    {
    }

    public void Render()
    {
        UIContext.Clear();

        if (Display != null)
            Display.Draw(UIContext);
        else RenderError();

        UIContext.Render();
    }

    private void RenderError()
    {
        ErrorDisplay ??= new FlexPanel(0, 0, UIContext.ViewportWidth, UIContext.ViewportHeight)
        {
            Background = new ColoredPlane((0.2f, 0.2f, 0.2f, 1f)),
            Direction = LayoutDirection.Vertical,
            HorizontalAlign = Align.Center,
            Padding = 50,
            Spacing = 10,
            Children =
            [
                new Label("Thirty Dollar Editor")
                {
                    FontSizePx = 36,
                    FontStyle = FontStyle.Bold,
                    Value = "If you're reading this the Editor has encountered an unrecoverable error."
                },
                new Label($"Error: {ErrorMessage}")
            ]
        };

        ErrorDisplay.Draw(UIContext);
    }

    public void Update()
    {
        try
        {
            var fps = 1 / Parent.UpdateTime;
            
            if (Stopwatch.GetElapsedTime(LastFpsUpdate) > FpsUpdateTimeout && FpsLabel != null)
            {
                LastFpsUpdate = Stopwatch.GetTimestamp();
                var value = FpsCounter.GetAverageFPS(fps);
                if (Math.Abs(value - LastFpsValue) > 0.1)
                {
                    FpsLabel.Value = $"FPS: {fps:##.00}";
                    LastFpsValue = value;
                }
            }

            CurrentCursor = CursorType.Normal;

            Display?.Update(UIContext);
            Parent.Cursor = CurrentCursor switch
            {
                CursorType.Pointer => MouseCursor.PointingHand,
                _ => MouseCursor.Default
            };
        }
        catch (Exception e)
        {
            ErrorMessage = "[Update]: " + e;
        }
    }

    public void Resize(int w, int h)
    {
        try
        {
            UIContext.Camera.Viewport = (w, h);
            UIContext.Camera.UpdateMatrix();

            if (Display != null)
            {
                Display.Width = w;
                Display.Height = h;
                Display.Layout();
            }

            if (ErrorDisplay == null) return;
            ErrorDisplay.Width = w;
            ErrorDisplay.Height = h;
            ErrorDisplay.Layout();
        }
        catch (Exception e)
        {
            ErrorMessage = "[Resize]: " + e;
        }
    }

    public void Close()
    {
    }

    public void FileDrop(string[] locations)
    {
        try
        {
        }
        catch (Exception e)
        {
            ErrorMessage = "[File Drop]: " + e;
        }
    }

    public void Keyboard(KeyboardState state)
    {
        try
        {
        }
        catch (Exception e)
        {
            ErrorMessage = "[Keyboard]: " + e;
        }
    }

    public void Mouse(MouseState mouse_state, KeyboardState keyboard_state)
    {
        try
        {
            Display?.Test(mouse_state);
        }
        catch (Exception e)
        {
            ErrorMessage = "[Mouse]: " + e;
        }
    }
}