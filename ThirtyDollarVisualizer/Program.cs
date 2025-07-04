// Warm thanks to The Cherno
// https://youtube.com/playlist?list=PLlrATfBNZ98foTJPJ_Ev03o2oq3-GGOS2

using System.Runtime.InteropServices;
using CommandLine;
using OpenTK.Windowing.Common.Input;
using SixLabors.ImageSharp.PixelFormats;
using ThirtyDollarVisualizer.Assets;
using ThirtyDollarVisualizer.Audio;
using ThirtyDollarVisualizer.Audio.BASS;
using ThirtyDollarVisualizer.Audio.Null;
using ThirtyDollarVisualizer.Audio.OpenAL;
using ThirtyDollarVisualizer.Base_Objects.Settings;
using ThirtyDollarVisualizer.Helpers.Logging;
using ThirtyDollarVisualizer.Scenes;
using ThirtyDollarVisualizer.Settings;
using Image = SixLabors.ImageSharp.Image;

namespace ThirtyDollarVisualizer;

public static class Program
{
    public static void Main(string[] args)
    {
        string? sequence = null;
        bool no_audio;
        AudioContext? audio_context = null;
        var width = 1600;
        var height = 840;
        var follow_mode = CameraFollowMode.TDWLike;
        int? fps = null;
        float? scale = null;
        string? greeting = null;
        int? event_size = null;
        int? event_margin = null;
        int? line_amount = null;
        string? settings_location = null;
        bool? transparent_framebuffer = null;
        string? mode = null;

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

                mode = options.Mode;

                follow_mode = options.CameraFollowMode switch
                {
                    "line" => CameraFollowMode.CurrentLine,
                    _ => CameraFollowMode.TDWLike
                };

                audio_context = no_audio
                    ? new NullAudioContext()
                    : options.AudioBackend switch
                    {
                        "null" => new NullAudioContext(),
                        "openal" => new OpenALContext(),
                        "bass" => new BassAudioContext(),
                        _ => null
                    };
            });

        if (sequence != null && !File.Exists(sequence))
        {
            DefaultLogger.Log("Program","Unable to find specified sequence. Running without a specified sequence.");
            sequence = null;
        }

        SettingsHandler.Load(settings_location ?? "./Settings.30$");
        var settings = SettingsHandler.Settings;

        if (settings.TransparentFramebuffer != transparent_framebuffer && transparent_framebuffer.HasValue)
            settings.TransparentFramebuffer = transparent_framebuffer.Value;

        if (line_amount.HasValue) settings.LineAmount = line_amount.Value;

        if (event_size.HasValue) settings.EventSize = event_size.Value;

        if (event_margin.HasValue) settings.EventMargin = event_margin.Value;

        var assetPath = Asset.Embedded("Textures/moai.png");
        using var assetStream = AssetManager.GetAsset(assetPath).Stream;
        var icon_stream = Image.Load<Rgba32>(assetStream);

        icon_stream.DangerousTryGetSinglePixelMemory(out var memory);
        var icon_bytes = MemoryMarshal.AsBytes(memory.Span).ToArray();
        var icon = new WindowIcon(
            new OpenTK.Windowing.Common.Input.Image(icon_stream.Width, icon_stream.Height, icon_bytes));

        Manager manager = null!;
        try
        {
            manager = new Manager(width, height, "Thirty Dollar Visualizer", fps, icon);
        }
        catch (Exception)
        {
            DefaultLogger.Log("Program", "Unable to create window. Trying without icon.");
            manager = new Manager(width, height, "Thirty Dollar Visualizer", fps);
        }
        
        if (manager.TryGetCurrentMonitorScale(out var horizontal_scale, out var vertical_scale) &&
            settings.AutomaticScaling) scale ??= (horizontal_scale + vertical_scale) / 2f;

        if (mode != null)
            settings.Mode = mode;

        if (settings.Mode == "Editor")
        {
            var thirty_dollar_editor = new ThirtyDollarEditor(width, height, settings, audio_context);
            manager.Scenes.Add(thirty_dollar_editor);
        }
        else
        {
            var tdw_application = new ThirtyDollarApplication(width, height, [sequence], settings, audio_context)
            {
                CameraFollowMode = follow_mode,
                Scale = scale ?? 1f,
                Greeting = greeting ?? settings.Greeting
            };

            manager.Scenes.Add(tdw_application);
        }

        manager.Run();
    }

    public class Options
    {
        [Option('i', "sequence", HelpText = "The sequence's location.")]
        public string? Input { get; set; }

        [Option("mode", HelpText = "Which mode the visualizer loads in.")]
        public string? Mode { get; set; }

        [Option("no-audio", HelpText = "Disable audio playback.")]
        public bool NoAudio { get; set; }


        [Option('w', "width", HelpText = "The width of the render window.")]
        public int? Width { get; set; }

        [Option('h', "height", HelpText = "The height of the render window.")]
        public int? Height { get; set; }

        [Option('c', "camera-follow-mode", HelpText = "Controls how the camera behaves. Values: \"tdw\", \"line\"")]
        public string? CameraFollowMode { get; set; }

        [Option('f', "fps-limit",
            HelpText = "The fps cap of the renderer. Valid values are 0 - 500. Setting this to 0 removes the fps cap.")]
        public int? Fps { get; set; }

        [Option('s', "scale",
            HelpText = "Changes the camera viewport zoom.")]
        public float? Scale { get; set; }

        [Option("audio-backend",
            HelpText = "Changes the audio backend the application uses. Values: \"bass\", \"openal\"")]
        public string? AudioBackend { get; set; }

        [Option("greeting",
            HelpText =
                "Changes the default \'DON'T LECTURE ME WITH YOUR THIRTY DOLLAR VISUALIZER\' greeting. Supports emojis.")]
        public string? Greeting { get; set; }

        [Option("event-size", HelpText = "Changes how big the events are in pixels.")]
        public int? EventSize { get; set; }

        [Option("event-margin", HelpText = "Changes the distance between events in pixels.")]
        public int? EventMargin { get; set; }

        [Option("line-amount", HelpText = "Changes how many events are on a single line.")]
        public int? LineAmount { get; set; }

        [Option("settings-location",
            HelpText = "Changes where the settings file is located. Default is: \'./Settings.30$\'")]
        public string? SettingsLocation { get; set; }

        [Option("transparent-framebuffer",
            HelpText =
                "Changes how the visualizer processes alpha rendering. If set the background of the window is rendered transparent and the OS decides how it'll use the transparency.")]
        public bool? TransparentFramebuffer { get; set; }
    }
}