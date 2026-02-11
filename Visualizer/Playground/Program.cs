using System.Reflection;
using Components;
using EditorScene;
using HomeScene;
using LoadingScene;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Playground;
using Shared;
using ThirtyDollarVisualizer.Engine;
using VisualizerScene;

Assembly[] assemblies =
[
    Assembly.GetExecutingAssembly(),
    SharedAssembly.Assembly,
    ComponentsAssembly.Assembly,
    LoaderAssembly.Assembly,
    HomeAssembly.Assembly,
    VisualizerAssembly.Assembly,
    EditorAssembly.Assembly
];

var game = new Game(assemblies, new GameWindowSettings(), new NativeWindowSettings
{
    ClientSize = (1024, 600),
    Vsync = VSyncMode.On,
    APIVersion = new Version(3, 3),
    Title = "Playground Window",
    Flags = ContextFlags.ForwardCompatible
}, "Playground");

game.Enqueue(instance =>
    instance.SceneManager.LoadScene<PlaygroundScene>("playground", manager => new PlaygroundScene(manager)));
game.Enqueue(instance => instance.SceneManager.TransitionTo("playground"));
game.Run();