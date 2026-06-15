using CommandLine;

namespace ThirtyDollarVisualizer;

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