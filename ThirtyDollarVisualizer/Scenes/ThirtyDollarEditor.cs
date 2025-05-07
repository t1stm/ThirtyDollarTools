using System.Diagnostics;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.Fonts;
using ThirtyDollarVisualizer.Audio;
using ThirtyDollarVisualizer.Audio.Null;
using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Renderer.Instanced;
using ThirtyDollarVisualizer.Settings;
using ThirtyDollarVisualizer.UI;
using ThirtyDollarVisualizer.UI.Components.File_Selector;

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
    private FlexPanel? MainPanel;
    private FlexPanel? ErrorDisplay;
    private CursorType CurrentCursor;

    private string ErrorMessage = "This message hasn't been updated yet. The error remains hidden...";

    public void Init(Manager manager)
    {
        Parent = manager;
        Display = new FlexPanel(width: InitialWidth, height: InitialHeight)
        {
            Direction = LayoutDirection.Vertical,
            Children =
            [
                new FlexPanel // Top Bar
                {
                    Background = new ColoredPlane
                    {
                        Color = (0.2f, 0.2f, 0.2f, 1f)
                    },
                    VerticalAlign = Align.Center,
                    AutoWidth = true,
                    Height = 32,
                    Padding = 10,
                    Spacing = 10,
                    Children =
                    [
                        new Label("Thirty Dollar Editor"),
                        new DropDownLabel("File", [
                            new Label("New"),
                            new Label("Open")
                            {
                                OnClick = _ =>
                                {
                                    var selection = new FileSelection
                                    {
                                        OnCancel = obj => { MainPanel?.Children.Remove(obj); }
                                    };
                                    MainPanel?.AddChild(selection);
                                }
                            },
                            new Label("Save"),
                            new Label("Save As"),
                        ])
                    ]
                },
                MainPanel = new FlexPanel() // Main Panel
                {
                    AutoWidth = true,
                    AutoHeight = true,
                    Background = new ColoredPlane
                    {
                        Color = (0.3f, 0.3f, 0.3f, 1f)
                    },
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

    private QuadArray? _holder;
    private PointerPlane[] _planes = null!;
    private Random random = new();

    private void CtrArr()
    {
        _holder = new QuadArray(16);
        _planes = _holder.ToPointerPlanes();
        
        UpdatePlanes();
    }

    private void UpdatePlanes()
    {
        var w = UIContext.Camera.Width;
        var h = UIContext.Camera.Height;
        
        foreach (var plane in _planes)
        {
            RandomizePlane(plane, w, h);
        }
    }

    private void RandomizePlane(PointerPlane plane, int w, int h)
    {
        plane.Position = (random.NextSingle() * (w - 100), random.NextSingle() * (h - 100), 0f);
        plane.Scale = (Math.Max(10, random.NextSingle() * 100), Math.Max(10, random.NextSingle() * 100), 0);
        plane.Color = (random.NextSingle(), random.NextSingle(), random.NextSingle(), 1f);
        plane.UpdateModel(false);
    }

    private long lastTimestamp;
    
    public void Render()
    {
        if (_holder == null)
            CtrArr();

        if (Stopwatch.GetElapsedTime(lastTimestamp).TotalMilliseconds > 1000)
        {
            var index = random.Next(0, _planes.Length);
            var plane = _planes[index];
            RandomizePlane(plane, UIContext.Camera.Width, UIContext.Camera.Height);

            lastTimestamp = Stopwatch.GetTimestamp();
        }
        
        _holder!.Render(UIContext.Camera);
    }

    private void RenderError()
    {
        ErrorDisplay ??= new FlexPanel(0, 0, UIContext.ViewportWidth, UIContext.ViewportHeight)
        {
            Background = new ColoredPlane
            {
                Color = (0.2f, 0.2f, 0.2f, 1f)
            },
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