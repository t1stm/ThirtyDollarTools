using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarConverter.Objects;
using ThirtyDollarVisualizer.Audio;
using ThirtyDollarVisualizer.Helpers.Timing;
using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Objects.Text;

namespace ThirtyDollarVisualizer.Scenes;

public class ThreeDollarWebsite : ThirtyDollarApplication
{
    private readonly ThreeDollarCamera _camera;
    private readonly ColoredPlane _ground;
    private readonly CachedDynamicText _text;
    private readonly TexturedPlane _texture;

    private readonly List<Renderable> objects_3d = new();
    private readonly DollarStoreCamera StaticCamera;
    private readonly SeekableStopwatch Stopwatch;
    private bool _first_move;

    private Vector2 _last_pos = Vector2.Zero;
    private int Height;
    private Manager Manager = null!;

    private int Width;

    public ThreeDollarWebsite(int width, int height, AudioContext? audio_context) : base(width, height, null,
        audio_context)
    {
        Width = width;
        Height = height;
        Stopwatch = new SeekableStopwatch();
        Stopwatch.Start();

        _camera = new ThreeDollarCamera(Vector3.UnitZ * 3, (float)Width / Height)
        {
            Fov = 60
        };

        StaticCamera = new DollarStoreCamera((0, 0, 0), (width, Height));

        _ground = new ColoredPlane((1, 1, 1, 1), (-0.5f, 0, -3), (0.5f, 0, -0.1f));
        _ground.UpdateModel(false);
        _text = new CachedDynamicText();
        _text.SetPosition((20, 20, 0));
        _texture = new TexturedPlane(new Texture("ThirtyDollarVisualizer.Assets.Textures.moai.png"), (1, 1, 1),
            (1.5f, 1.5f, 1.5f));
    }

    public override void Init(Manager manager)
    {
        Manager = manager;
        static_objects.Add(_text);
        objects_3d.Add(_ground);
        objects_3d.Add(_texture);

        Manager.CursorState = CursorState.Grabbed;
    }

    protected override void HandleAfterSequenceLoad(TimedEvents events)
    {
    }

    public override void Render()
    {
        _ground.SetPosition((0f, 0, 0));
        _ground.SetScale((1, 0.2f, 1));
        _ground.UpdateModel(false);
        _texture.UpdateModel(false);

        _text.SetTextContents(
            $"""
             Open Time: {Stopwatch.Elapsed:hh\:mm\:ss\.fff}

             Ground: Position: {_ground.GetPosition()}, Scale: {_ground.GetScale()}
             Texture: Position: {_texture.GetPosition()}, Scale: {_texture.GetScale()}

             Camera:

             Position: {_camera.Position}, Yaw: {_camera.Yaw},
             Pitch: {_camera.Pitch}, FOV: {_camera.Fov}
             """);

        foreach (var renderable in static_objects) renderable.Render(StaticCamera);

        foreach (var renderable in objects_3d) renderable.Render(_camera);
    }

    public override void Resize(int w, int h)
    {
        Width = w;
        Height = h;

        _camera.AspectRatio = (float)w / h;
        GL.Viewport(0, 0, w, h);
    }

    public override void Mouse(MouseState mouse_state, KeyboardState keyboard_state)
    {
        const float sensitivity = 0.05f;

        if (_first_move) // This bool variable is initially set to true.
        {
            _last_pos = new Vector2(mouse_state.X, mouse_state.Y);
            _first_move = false;
        }
        else
        {
            // Calculate the offset of the mouse position
            var deltaX = mouse_state.X - _last_pos.X;
            var deltaY = mouse_state.Y - _last_pos.Y;
            _last_pos = new Vector2(mouse_state.X, mouse_state.Y);

            // Apply the camera pitch and yaw (we clamp the pitch in the camera class)
            _camera.Yaw += deltaX * sensitivity;
            _camera.Pitch -= deltaY * sensitivity; // Reversed since y-coordinates range from bottom to top
        }
    }

    public override void Keyboard(KeyboardState state)
    {
        const float cameraSpeed = 0.2f;

        if (state.IsKeyDown(Keys.Left)) _camera.Yaw -= cameraSpeed;

        if (state.IsKeyDown(Keys.Right)) _camera.Yaw += cameraSpeed;

        if (state.IsKeyDown(Keys.Up)) _camera.Pitch -= cameraSpeed;

        if (state.IsKeyDown(Keys.Down)) _camera.Pitch += cameraSpeed;

        if (state.IsKeyDown(Keys.W)) _camera.Position += _camera.Front * cameraSpeed * 0.05f; // Forward

        if (state.IsKeyDown(Keys.S)) _camera.Position -= _camera.Front * cameraSpeed * 0.05f; // Backwards
        if (state.IsKeyDown(Keys.A)) _camera.Position -= _camera.Right * cameraSpeed * 0.05f; // Left
        if (state.IsKeyDown(Keys.D)) _camera.Position += _camera.Right * cameraSpeed * 0.05f; // Right
        if (state.IsKeyDown(Keys.Space)) _camera.Position += _camera.Up * cameraSpeed * 0.05f; // Up
        if (state.IsKeyDown(Keys.LeftShift)) _camera.Position -= _camera.Up * cameraSpeed * 0.05f; // Down
    }
}