using System.Reflection;
using EditorScene;
using HomeScene;
using LoadingScene;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Playground;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;
using Shared;
using Sundex.Components;
using Sundex.Engine;
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


#if RELEASE
const string logFilePath = "Visualizer_Release.log";
#endif
#if DEBUG
const string logFilePath = "Visualizer_Debug.log";
#endif

var serilogLogger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new ExpressionTemplate(
        "[{@t:HH:mm:ss} {@l:u3}" +
        "{#if SourceContext is not null} {Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}{#end}] {@m}\n{@x}",
        theme: TemplateTheme.Code))
    .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day,
        rollOnFileSizeLimit: true, fileSizeLimitBytes: 100_000_000)
    .MinimumLevel.Debug()
    .CreateLogger();

var game = new Game(serilogLogger, assemblies, new GameWindowSettings(), new NativeWindowSettings
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