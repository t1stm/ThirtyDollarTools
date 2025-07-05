using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.Fonts;
using ThirtyDollarVisualizer.Assets;
using ThirtyDollarVisualizer.Audio;
using ThirtyDollarVisualizer.Audio.Null;
using ThirtyDollarVisualizer.Base_Objects;
using ThirtyDollarVisualizer.Base_Objects.Planes;
using ThirtyDollarVisualizer.Base_Objects.Textures.Atlas;
using ThirtyDollarVisualizer.Base_Objects.Textures.Static;
using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Settings;
using ThirtyDollarVisualizer.UI.Abstractions;
using ThirtyDollarVisualizer.UI.Components.File_Selector;
using ThirtyDollarVisualizer.UI.Components.Labels;
using ThirtyDollarVisualizer.UI.Components.Panels;

namespace ThirtyDollarVisualizer.Scenes;

public class ThirtyDollarEditor(int width, int height, VisualizerSettings settings, AudioContext? audioContext)
    : IScene
{
    private readonly AudioContext _audioContext = audioContext ?? new NullAudioContext();

    private readonly VisualizerSettings _settings = settings;

    private readonly UIContext _uiContext = new()
    {
        Camera = new DollarStoreCamera((0, 0, 0), (width, height))
    };

    private CursorType _currentCursor;

    private FlexPanel? _display;
    private FlexPanel? _errorDisplay;

    private string _errorMessage = "This message hasn't been updated yet. The error remains hidden...";
    private FlexPanel? _mainPanel;
    private Manager _parent = null!;

    public void Init(Manager manager)
    {
        _parent = manager;
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

    public void Start()
    {
    }

    public void Render()
    {
        RenderError();
    }

    public void Update()
    {
        try
        {
            _currentCursor = CursorType.Normal;

            _display?.Update(_uiContext);
            _parent.Cursor = _currentCursor switch
            {
                CursorType.Pointer => MouseCursor.PointingHand,
                _ => MouseCursor.Default
            };
        }
        catch (Exception e)
        {
            _errorMessage = "[Update]: " + e;
        }
    }

    public void Resize(int w, int h)
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
            _errorMessage = "[File Drop]: " + e;
        }
    }

    public void Keyboard(KeyboardState state)
    {
        try
        {
            if (state.IsKeyPressed(Keys.Space)) debugMarker++;
        }
        catch (Exception e)
        {
            _errorMessage = "[Keyboard]: " + e;
        }
    }

    public void Mouse(MouseState mouseState, KeyboardState keyboardState)
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

    private static TexturedPlane? rndbl;
    private static ImageAtlas? atlas;
    private int oldMarker = 0;
    private int debugMarker = 0;
    
    private void RenderError()
    {
        atlas ??= new ImageAtlas();
        rndbl ??= new TexturedPlane(atlas)
        {
            Color = Vector4.One,
            Position = Vector3.Zero
        };
        
        if (oldMarker != debugMarker)
        {
            oldMarker = debugMarker;
            var img = new StaticTexture("Assets/Textures/moai.png");
            var data = img.GetData();
            
            atlas.AddImage(data!);
        }

        rndbl.Scale = new Vector3((_uiContext.ViewportWidth, _uiContext.ViewportHeight));
        rndbl.Render(_uiContext.Camera);
    }
}