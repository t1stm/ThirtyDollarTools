// Warm thanks to The Cherno
// https://youtube.com/playlist?list=PLlrATfBNZ98foTJPJ_Ev03o2oq3-GGOS2

#region Usings

using System.Reflection;
using CommandLine;
using DrumMasterScene;
using EditorScene;
using HomeScene;
using LoadingScene;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;
using SettingsScene;
using Shared;
using Shared.Audio;
using Shared.Audio.BASS;
using Shared.Audio.Null;
using Shared.Audio.OpenAL;
using SixLabors.ImageSharp;
using Sundex.Components;
using Sundex.Engine;
using ThirtyDollarVisualizer;
using ThirtyDollarVisualizer.VisualizerSettings;
using VisualizerScene;

#endregion

string? sequence = null;
bool no_audio;
AudioContext? audio_context = null;
var width = 1600;
var height = 840;
int? fps = null;
float? scale = null;
string? greeting = null;
int? event_size = null;
int? event_margin = null;
int? line_amount = null;
string? settings_location = null;
bool? transparent_framebuffer = null;

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


Parser.Default.ParseArguments<Options>(args)
    .WithParsed(options =>
    {
        sequence = options.Input;
        no_audio = options.NoAudio;
        width = options.Width ?? width;
        height = options.Height ?? height;
        fps = options.Fps;
        scale = options.Scale;
        greeting = options.Greeting;
        event_size = options.EventSize;
        event_margin = options.EventMargin;
        line_amount = options.LineAmount;
        settings_location = options.SettingsLocation;
        transparent_framebuffer = options.TransparentFramebuffer;

        audio_context = no_audio
            ? new NullAudioContext()
            : options.AudioBackend switch
            {
                "null" => new NullAudioContext(),
                "openal" => new OpenALContext(serilogLogger),
                "bass" => new BassAudioContext(serilogLogger),
                _ => null
            };
    });

Configuration.Default.PreferContiguousImageBuffers = true;

if (sequence != null && !File.Exists(sequence))
{
    serilogLogger.Warning("Unable to find specified sequence. Running without a specified sequence");
    sequence = null;
}

SettingsHandler.Load(settings_location ?? "./Settings.30$");
var settings = SettingsHandler.Settings;

if (settings.TransparentFramebuffer != transparent_framebuffer && transparent_framebuffer.HasValue)
    settings.TransparentFramebuffer = transparent_framebuffer.Value;

if (line_amount.HasValue) settings.LineAmount = line_amount.Value;
if (event_size.HasValue) settings.EventSize = event_size.Value;
if (event_margin.HasValue) settings.EventMargin = event_margin.Value;

var gameWindowSettings = new GameWindowSettings
{
    UpdateFrequency = fps ?? 0
};

// ReSharper disable once ConvertToConstant.Local
var contextFlags = ContextFlags.ForwardCompatible;

#if DEBUG
contextFlags |= ContextFlags.Debug;
#endif

if (fps == null && !settings.UseVsync)
    fps = 0;

var nativeWindowSettings = new NativeWindowSettings
{
    Icon = null,
    API = ContextAPI.OpenGL,
    Profile = ContextProfile.Core,
    Flags = contextFlags,
    APIVersion = new Version(3, 3),
    Title = "Thirty Dollar Visualizer",
    WindowState = WindowState.Normal,
    WindowBorder = WindowBorder.Resizable,
    TransparentFramebuffer = settings.TransparentFramebuffer,
    Vsync = fps == null ? VSyncMode.On : VSyncMode.Off,
    ClientSize = (width, height)
};

Assembly[] assemblies =
[
    Assembly.GetExecutingAssembly(),
    SharedAssembly.Assembly,
    ComponentsAssembly.Assembly,
    LoaderAssembly.Assembly,
    HomeAssembly.Assembly,
    VisualizerAssembly.Assembly,
    EditorAssembly.Assembly,
    DrumMasterAssembly.Assembly,
    SettingsAssembly.Assembly
];

var game = new Game(serilogLogger, assemblies, gameWindowSettings, nativeWindowSettings,
    "ThirtyDollarVisualizer");
game.Globals.Set("visualizer-settings", settings);

if (game.TryGetScreenScale(out var horizontal_scale, out var vertical_scale) &&
    settings.AutomaticScaling) scale ??= (horizontal_scale + vertical_scale) / 2f;

game.Enqueue(instance =>
{
    instance.SceneManager.LoadScene<Loader>("loader",
        _ => new Loader(instance, audio_context)
        {
            OnFinish = workflow => { OnLoadHandler(instance, workflow, sequence, greeting, scale); }
        });
});

game.Enqueue(instance => instance.SceneManager.TransitionTo("loader"));
game.Run();

return;

static void OnLoadHandler(Game game, ThirtyDollarWorkflow workflow,
    string? sequence, string? greeting, float? scale)
{
    // preload all scenes in memory (inefficient i know, but leads to better UX)
    game.Enqueue(instance =>
    {
        instance.SceneManager.LoadScene<Home>("home", _ => new Home(instance, Visualizer.GetVersion(instance.AssetProvider)));

        instance.SceneManager.LoadScene<Visualizer>("visualizer", _ =>
            new Visualizer(instance, SettingsHandler.Settings, workflow, [sequence])
            {
                Greeting = greeting,
                Scale = scale ?? 1f
            }
        );

        instance.SceneManager.LoadScene<DrumMaster>("drum-master", _ => new DrumMaster(instance, workflow));
        instance.SceneManager.LoadScene<Editor>("editor", _ => new Editor(instance, workflow));
        instance.SceneManager.LoadScene<Settings>("settings", _ => new Settings(instance, SettingsHandler.Settings));
    });

    game.Enqueue(instance => instance.SceneManager.TransitionTo("home"));
}