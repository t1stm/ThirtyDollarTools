// Warm thanks to The Cherno
// https://youtube.com/playlist?list=PLlrATfBNZ98foTJPJ_Ev03o2oq3-GGOS2

using System.Reflection;
using System.Runtime.InteropServices;
using CommandLine;
using OpenTK.Windowing.Common.Input;
using SixLabors.ImageSharp.PixelFormats;
using ThirtyDollarVisualizer.Audio;
using ThirtyDollarVisualizer.Audio.Null;
using ThirtyDollarVisualizer.Objects.Settings;
using ThirtyDollarVisualizer.Scenes;
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
        var follow_mode = CameraFollowMode.TDW_Like;
        int? fps = null;
        float? scale = null;
        string? greeting = null;
        int? event_size = null;
        int? event_margin = null;
        int? line_amount = null;

        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(options =>
            {
                sequence = options.Input;
                no_audio = options.NoAudio;
                width = options.Width ?? width;
                height = options.Height ?? height;
                fps = options.FPS;
                scale = options.Scale;
                greeting = options.Greeting;
                event_size = options.EventSize;
                event_margin = options.EventMargin;
                line_amount = options.LineAmount;

                follow_mode = options.CameraFollowMode switch
                {
                    "line" => CameraFollowMode.Current_Line,
                    _ => CameraFollowMode.TDW_Like
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
            Console.WriteLine("Unable to find specified sequence. Running without a specified sequence.");
            sequence = null;
        }

        var icon_stream = Image.Load<Rgba32>(Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("ThirtyDollarVisualizer.Assets.Textures.moai.png")!);

        icon_stream.DangerousTryGetSinglePixelMemory(out var memory);
        var icon_bytes = MemoryMarshal.AsBytes(memory.Span).ToArray();
        var icon = new WindowIcon(
            new OpenTK.Windowing.Common.Input.Image(icon_stream.Width, icon_stream.Height, icon_bytes));

        var manager = new Manager(width, height, "Thirty Dollar Visualizer", fps, icon);

        var tdw_application = new ThirtyDollarApplication(width, height, new [] { sequence }, audio_context)
        {
            CameraFollowMode = follow_mode,
            Scale = scale ?? 1f,
            Greeting = greeting,
            ElementsOnSingleLine = line_amount ?? 16,
            RenderableSize = event_size ?? 64,
            MarginBetweenRenderables = event_margin ?? 12
        };

        manager.Scenes.Add(tdw_application);

        /*var un30_dollar_application = new ThreeDollarWebsite(width, height, audio_context);
        manager.Scenes.Add(un30_dollar_application);*/

        manager.Run();
    }

    public class Options
    {
        [Option('i', "sequence", HelpText = "The sequence's location.")]
        public string? Input { get; set; }

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
        public int? FPS { get; set; }

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
    }
}