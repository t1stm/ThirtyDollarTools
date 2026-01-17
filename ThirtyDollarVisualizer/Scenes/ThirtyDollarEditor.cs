using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarVisualizer.Audio;
using ThirtyDollarVisualizer.Audio.Null;
using ThirtyDollarVisualizer.Base_Objects.Planes;
using ThirtyDollarVisualizer.Engine.Scenes;
using ThirtyDollarVisualizer.Engine.Scenes.Arguments;
using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Settings;
using ThirtyDollarVisualizer.UI.Abstractions;
using ThirtyDollarVisualizer.UI.Components.File_Selector;
using ThirtyDollarVisualizer.UI.Components.Labels;
using ThirtyDollarVisualizer.UI.Components.Panels;

namespace ThirtyDollarVisualizer.Scenes;

public class ThirtyDollarEditor(SceneManager sceneManager, VisualizerSettings settings, AudioContext? audioContext): Scene(sceneManager)
{
    private readonly AudioContext _audioContext = audioContext ?? new NullAudioContext();
    private readonly VisualizerSettings _settings = settings;

    private readonly UIContext _uiContext = new()
    {
        Camera = new DollarStoreCamera((0, 0, 0), (0, 0))
    };

    private CursorType _currentCursor;

    private FlexPanel? _display;
    private FlexPanel? _errorDisplay;

    private string _errorMessage = "This message hasn't been updated yet. The error remains hidden...";
    private FlexPanel? _mainPanel;

    public override void Initialize(InitArguments initArguments)
    {
        var width = initArguments.StartingResolution.X;
        var height = initArguments.StartingResolution.Y;
        
        _display = new FlexPanel(width: width, height: height)
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
                                        OnCancel = obj => { _mainPanel?.Children.Remove(obj); }
                                    };
                                    _mainPanel?.AddChild(selection);
                                }
                            },
                            new Label("Save"),
                            new Label("Save As")
                        ])
                    ]
                },
                _mainPanel = new FlexPanel() // Main Panel
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
        _uiContext.RequestCursor = cursor => { _currentCursor = cursor; };

        _display.Layout();
    }

    public override void Start()
    {
    }

    public override void Render(RenderArguments args)
    {
        RenderError();
    }

    public override void TransitionedTo()
    {
        // Does nothing for now.
    }
    
    public override void Update(UpdateArguments updateArgs)
    {
        try
        {
            _currentCursor = CursorType.Normal;
            _display?.Update(_uiContext);
        }
        catch (Exception e)
        {
            _errorMessage = "[Update]: " + e;
        }
    }

    public override void Resize(int w, int h)
    {
        try
        {
            _uiContext.Camera.Viewport = (w, h);
            _uiContext.Camera.UpdateMatrix();

            if (_display != null)
            {
                _display.Width = w;
                _display.Height = h;
                _display.Layout();
            }

            if (_errorDisplay == null) return;
            _errorDisplay.Width = w;
            _errorDisplay.Height = h;
            _errorDisplay.Layout();
        }
        catch (Exception e)
        {
            _errorMessage = "[Resize]: " + e;
        }
    }

    public override void Shutdown()
    {
    }

    public override void FileDrop(string[] locations)
    {
        try
        {
        }
        catch (Exception e)
        {
            _errorMessage = "[File Drop]: " + e;
        }
    }

    public override void Keyboard(KeyboardState state)
    {
        try
        {
            
        }
        catch (Exception e)
        {
            _errorMessage = "[Keyboard]: " + e;
        }
    }

    public override void Mouse(MouseState mouseState, KeyboardState keyboardState)
    {
        try
        {
            _display?.Test(mouseState);
        }
        catch (Exception e)
        {
            _errorMessage = "[Mouse]: " + e;
        }
    }
    
    private void RenderError()
    {
        
    }
}