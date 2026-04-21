using System.Reflection;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;
using Sundex.Engine;
using Sundex.Engine.DummyProject;

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
    .CreateLogger()
    .ForContext<Game>();

var game = new Game(serilogLogger, [Assembly.GetExecutingAssembly()], new GameWindowSettings(), new NativeWindowSettings
{
    ClientSize = (1024, 600),
    Vsync = VSyncMode.On,
    APIVersion = new Version(3, 3),
    Title = "Thirty Dollar Visualizer",
    Flags = ContextFlags.ForwardCompatible
}, "Dummy");

var scene = game.SceneManager.LoadScene<DummyScene>("dummy", _ => new DummyScene(game));
game.SceneManager.TransitionTo(scene);
game.Run();