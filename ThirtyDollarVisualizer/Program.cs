// Warm thanks to The Cherno
// https://youtube.com/playlist?list=PLlrATfBNZ98foTJPJ_Ev03o2oq3-GGOS2

using CommandLine;
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
    }
    
    public static void Main(string[] args)
    {
        string? composition = null;
        var no_audio = false;

        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(options =>
            {
                composition = options.Input;
                no_audio = options.NoAudio;

            });

        if (composition == null) return;
        
        var manager = new Manager(1600,840, "Thirty Dollar Visualizer");

        var tdw_application = new ThirtyDollarApplication(1600, 840, composition)
        {
            PlayAudio = !no_audio
        };

        manager.Scenes.Add(tdw_application);
        
        manager.Run();
    }
}