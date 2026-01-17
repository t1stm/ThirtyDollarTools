using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarVisualizer.Engine.Renderer.Cameras;
using ThirtyDollarVisualizer.Engine.Scenes;
using ThirtyDollarVisualizer.Engine.Scenes.Arguments;
using ThirtyDollarVisualizer.Engine.Text;
using ThirtyDollarVisualizer.Engine.Text.Fonts;

namespace ThirtyDollarVisualizer.Engine.DummyProject;

public class DummyScene(SceneManager sceneManager) : Scene(sceneManager)
{
    private TextProvider _textProvider = null!;
    private FontProvider _fontProvider = null!;
    private TextBuffer _textBuffer = null!;

    private TextSlice _textSlice = null!;
    private Camera _camera = null!;
    private Vector2 _dvdDirection = Vector2.One;

    public override void Initialize(InitArguments initArguments)
    {
        _fontProvider = new FontProvider(AssetProvider);
        _textProvider = new TextProvider(AssetProvider, _fontProvider, "Lato Regular");
        _textBuffer = new TextBuffer(_textProvider);

        _textSlice = _textBuffer.GetTextSlice(
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit. \n" +
            "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. \n" +
            "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. \n" +
            "Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. \n" +
            "Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum");
        _textSlice.Position = (100f, 100f, 0f);

        _camera = new DummyCamera(Vector3.Zero, new Vector2i(1024, 600));
        _camera.UpdateMatrix();
    }

    public override void Start()
    {
    }

    public override void Render(RenderArguments renderArgs)
    {
        _textBuffer.RenderBuffer(_camera);
    }

    public override void TransitionedTo()
    {
    }

    public override void Update(UpdateArguments updateArgs)
    {
        const float travelDistance = 3f;
        _textSlice.Position += new Vector3(_dvdDirection * travelDistance);
        if (_textSlice.Position.X > _camera.Width || _textSlice.Position.X < 0) _dvdDirection.X = -_dvdDirection.X;
        if (_textSlice.Position.Y > _camera.Height || _textSlice.Position.Y < 0) _dvdDirection.Y = -_dvdDirection.Y;
    }

    public override void Resize(int w, int h)
    {
        _camera.Viewport = new Vector2i(w, h);
        _camera.UpdateMatrix();
    }

    public override void Shutdown()
    {
    }

    public override void FileDrop(string[] locations)
    {
    }

    public override void Mouse(MouseState mouseState, KeyboardState keyboardState)
    {
    }

    public override void Keyboard(KeyboardState state)
    {
        if (state.IsKeyPressed(Keys.R))
        {
            _textSlice.Dispose();
            _textSlice = _textBuffer.GetTextSlice("Hello World!");
            _textSlice.Position = (600f, 600f, 0f);
        }

        if (state.IsKeyPressed(Keys.G))
        {
            // Force GC
            GC.Collect();
        }
    }
}