using System.Reflection;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using ThirtyDollarVisualizer.Engine;
using ThirtyDollarVisualizer.Engine.DummyProject;

var game = new Game(Assembly.GetExecutingAssembly(), new GameWindowSettings(), new NativeWindowSettings
{
    ClientSize = (1024, 600),
    Vsync = VSyncMode.On,
    APIVersion = new Version(3, 3),
    Title = "Thirty Dollar Visualizer",
    Flags = ContextFlags.ForwardCompatible
});

var scene = game.SceneManager.LoadScene<DummyScene>("dummy", manager => new DummyScene(manager));
game.SceneManager.TransitionTo(scene);
game.Run();