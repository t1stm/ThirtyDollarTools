// Warm thanks to The Cherno
// https://youtube.com/playlist?list=PLlrATfBNZ98foTJPJ_Ev03o2oq3-GGOS2

using CommandLine;
using ThirtyDollarVisualizer.Objects.Settings;
using ThirtyDollarVisualizer.Scenes;

namespace ThirtyDollarVisualizer;

public static class Program
{
    public class Options
    {
        [Option('i', "composition", Required = true, HelpText = "The composition's location.")]
        public string? Input { get; set; }

        [Option("no-audio", HelpText = "Disable audio playback.")]
        public bool NoAudio { get; set; }
        
        [Option('w', "width", HelpText = "The width of the render window.")]
        public int? Width { get; set; }
        
        [Option('h', "height", HelpText = "The height of the render window.")]
        public int? Height { get; set; }

        [Option('c', "camera_follow_mode", HelpText = "Controls how the camera behaves. Values: \"tdw\", \"line\"")]
        public string? CameraFollowMode { get; set; }

        [Option('f', "fps-limit",
            HelpText = "The fps cap of the renderer. Valid values are 0 - 500. Setting this to 0 removes the fps cap.")]
        public int? FPS { get; set; }
        
        [Option('s', "scale",
            HelpText = "Changes the camera viewport zoom.")]
        public int? Scale { get; set; }
    }
    
    public static void Main(string[] args)
    {
        string? composition = null;
        var no_audio = false;
        var width = 1600;
        var height = 840;
        var follow_mode = CameraFollowMode.TDW_Like;
        int? fps = null;
        float? scale = null;

        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(options =>
            {
                composition = options.Input;
                no_audio = options.NoAudio;
                width = options.Width ?? width;
                height = options.Height ?? height;
                fps = options.FPS ?? 60;
                scale = options.Scale;
                
                follow_mode = options.CameraFollowMode switch
                {
                    "line" => CameraFollowMode.Current_Line,
                    _ => CameraFollowMode.TDW_Like
                };
            });

        if (composition == null) return;
        
        var manager = new Manager(width, height, "Thirty Dollar Visualizer", fps);

        var tdw_application = new ThirtyDollarApplication(width, height, composition)
        {
            PlayAudio = !no_audio,
            CameraFollowMode = follow_mode,
            Scale = scale ?? 1f
        };

        manager.Scenes.Add(tdw_application);
        
        manager.Run();
    }
}