// Warm thanks to The Cherno
// https://youtube.com/playlist?list=PLlrATfBNZ98foTJPJ_Ev03o2oq3-GGOS2

#region Usings

using System.Reflection;
using CommandLine;
using LoadingScene;
using Shared;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;
using SixLabors.ImageSharp;
using Sundex.Engine;
using Sundex.Engine.Audio;
using ThirtyDollarVisualizer;
using ThirtyDollarVisualizer.VisualizerSettings;
using VisualizerScene.Settings;

#endregion

string? sequence = null;
string? mode = null;
bool no_audio;
AudioDevice? audio_device = null;
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
        mode = options.Mode?.ToLowerInvariant();
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

        audio_device = AudioDevice.Open(serilogLogger, silent: no_audio || options.AudioBackend == "null");
    });

Configuration.Default.PreferContiguousImageBuffers = true;

if (sequence != null && !File.Exists(sequence))
{
    serilogLogger.Warning("Unable to find specified sequence. Running without a specified sequence");
    sequence = null;
}

SettingsHandler.Load(settings_location ?? "./Settings.30$");
var settings = SettingsHandler.Settings;

// The shortcut table follows the settings object from here; nothing else has to be told.
Keybinds.Attach(settings);

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

using var game = new Game(serilogLogger, VisualizerStartup.GetAssetAssemblies(Assembly.GetExecutingAssembly()),
    gameWindowSettings, nativeWindowSettings, "ThirtyDollarVisualizer");

if (game.TryGetScreenScale(out var horizontal_scale, out var vertical_scale) &&
    settings.AutomaticScaling) scale ??= (horizontal_scale + vertical_scale) / 2f;

#if DEBUG
ScenePreload[] debugPreloads =
[
    // Only --mode text-bench opens it, and it builds nothing until it is shown.
    new("Preparing the text benchmark", (runtime, _) =>
        runtime.SceneManager.LoadScene<TextBenchScene>("text-bench", _ => new TextBenchScene(runtime))),

    // Only --mode gpu-mix-bench opens it; it reads -i once it is shown.
    new("Preparing the GPU mixer benchmark", (runtime, workflow) =>
        runtime.SceneManager.LoadScene<GpuMixBenchScene>("gpu-mix-bench",
            _ => new GpuMixBenchScene(runtime, workflow, sequence)))
];
#else
ScenePreload[] debugPreloads = [];
#endif

VisualizerStartup.Start(game.Runtime, audio_device, settings, new Workspace(), mode, sequence, greeting, scale,
    debugPreloads);
game.Run();
