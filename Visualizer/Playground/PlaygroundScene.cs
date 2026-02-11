using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Shared.Renderer.Planes.Uniforms;
using ThirtyDollarVisualizer.Engine.Renderer.Cameras;
using ThirtyDollarVisualizer.Engine.Scenes;
using ThirtyDollarVisualizer.Engine.Scenes.Arguments;
using VisualizerScene.Objects;

namespace Playground;

public class PlaygroundScene(SceneManager sceneManager) : Scene(sceneManager)
{
    private readonly List<GradientPlane> _gradientPlanes = [];
    private readonly Camera _camera =
        new DollarStoreCamera(Vector3.Zero, (sceneManager.Game.ClientSize.X, sceneManager.Game.ClientSize.Y));

    public override void Initialize(InitArguments initArguments)
    {
        var types = Enum.GetValues<GradientType>();
        const float spacing = 550f;
        var startX = -(types.Length - 1) * spacing / 2f;

        for (var i = 0; i < types.Length; i++)
        {
            var type = types[i];
            var plane = new GradientPlane
            {
                Position = new Vector3(startX + i * spacing, 0, 0),
                Scale = new Vector3(500, 500, 1),
                GradientType = type,
                GradientColors =
                [
                    new Vector4(1, 0, 0, 0.6f),
                    new Vector4(0, 1, 0, 0.6f),
                    new Vector4(0, 0, 0, 0.6f)
                ],
                GradientStops = [0f, 0.5f, 1f],
                BorderRadius = 50f
            };
            _gradientPlanes.Add(plane);
        }
    }

    public override void Start()
    {
    }

    public override void Render(RenderArguments renderArgs)
    {
        foreach (var plane in _gradientPlanes)
        {
            plane.Render(_camera);
        }
    }

    public override void TransitionedTo()
    {
    }

    public override void Update(UpdateArguments updateArgs)
    {
        _camera.UpdateMatrix();
        foreach (var plane in _gradientPlanes)
        {
            plane.Update();
        }
    }

    public override void Resize(int w, int h)
    {
        _camera.Viewport = new Vector2i(w, h);
    }

    public override void Shutdown()
    {
    }

    public override void FileDrop(string[] locations)
    {
    }

    public override void Keyboard(KeyboardState state)
    {
    }

    public override void Mouse(MouseState mouseState, KeyboardState keyboardState)
    {
    }
}