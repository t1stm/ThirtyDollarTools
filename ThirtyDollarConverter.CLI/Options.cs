using CommandLine;

namespace ThirtyDollarConverter.CLI;

public class Options
{
    [Option('i', "input", HelpText = "The sequence locations.", Required = true)]
    public IEnumerable<string> Input { get; set; } = null!;

    [Option('o', "output", HelpText = "The exported audio locations.")]
    public IEnumerable<string> Output { get; set; } = null!;
    
    [Option('s', "sample-rate", HelpText = "Changes the exported audio's sample rate.")]
    public int? SampleRate { get; set; }
    
    [Option("download-location", HelpText = "Changes the exported audio's sample rate.")]
    public string? DownloadLocation { get; set; }
}