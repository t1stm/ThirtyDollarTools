using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Renderer.Planes;
using Sundex.Components.Color_Scheme;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;

namespace Playground;

public class PlaygroundScene(SceneManager sceneManager) : Scene(sceneManager)
{
    private readonly Camera _camera =
        new DollarStoreCamera(Vector3.Zero, (sceneManager.Game.ClientSize.X, sceneManager.Game.ClientSize.Y));

    private readonly List<GradientPlane> _gradientPlanes = [];

    public override void Initialize(InitArguments initArguments)
    {
        const float spacing = 550f;
        const int startX = 0;

        var gradient1 = GradientPlaneDefinitions.NewAccentBlueRadial();
        gradient1.Position = new Vector3(startX, 0, 0);
        gradient1.Scale = new Vector3(500, 500, 1);

        var gradient2 = GradientPlaneDefinitions.NewMagentaBlueRadial();
        gradient2.Position = new Vector3(startX + spacing, 0, 0);
        gradient2.Scale = new Vector3(500, 500, 1);

        var gradient3 = GradientPlaneDefinitions.NewTealToBgSurfaceRadial();
        gradient3.Position = new Vector3(startX + spacing * 2, 0, 0);
        gradient3.Scale = new Vector3(500, 500, 1);

        _gradientPlanes.Add(gradient1);
        _gradientPlanes.Add(gradient2);
        _gradientPlanes.Add(gradient3);
    }

    public override void Start()
    {
    }

    public override void Render(RenderArguments renderArgs)
    {
        foreach (var plane in _gradientPlanes) plane.Render(_camera);
    }

    public override void TransitionedTo()
    {
    }

    public override void Update(UpdateArguments updateArgs)
    {
        _camera.UpdateMatrix();
        foreach (var plane in _gradientPlanes) plane.Update();
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