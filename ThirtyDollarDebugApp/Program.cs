using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Resamplers;
using ThirtyDollarParser;

namespace ThirtyDollarApp;

internal static class Program
{
    private const char EmptyBlock = '□', FullBlock = '■';

    private static async Task Main(string[] args)
    {
        const string workingDirectory = "/home/kris/RiderProjects/ThirtyDollarTools/ThirtyDollarDebugApp";
        const string sequenceDirectory = $"{workingDirectory}/Included Sequences";

        var holder = new SampleHolder
        {
            DownloadLocation = $"{workingDirectory}/Sounds"
        };
        await holder.LoadSampleList();
        await holder.DownloadSamples();
        holder.LoadSamplesIntoMemory();
        Console.Clear();

        var list = Directory.GetFiles(sequenceDirectory).ToList();

        Directory.CreateDirectory("./Export");

        var output = new List<CompositionFile>();
        if (args.Length > 0)
        {
            output.AddRange(await ReadFileList(args));
        }
        else
        {
            output.AddRange(await ReadFileList(list));
        }

        const int statusbar_length = 64;
        foreach (var file in output)
        {
            if (file.Location.Contains("LICENSE")) continue;
            var composition = Composition.FromString(file.Data);

            var encoder = new PcmEncoder(holder, new EncoderSettings
            {
                SampleRate = 48000,
                Channels = 2,
                CutDelayMs = 300,
                Resampler = new LinearResampler()
            }, Console.WriteLine, (current, total) =>
            {
                ClearLine();
                Console.Write(GenerateProgressbar(current, (long) total, statusbar_length));
            });

            var audioData = encoder.SampleComposition(composition); // Shame on me...
            encoder.WriteAsWavFile($"./Export/{file.Location.Split('/').Last()}.wav", audioData);
        }

        Console.WriteLine("Finished Executing. Press any key to exit.");
        Console.ReadLine();
    }

    private static void ClearLine()
    {
        do { Console.Write("\b \b"); } while (Console.CursorLeft > 0);
    }
    
    private static async Task<List<CompositionFile>> ReadFileList(IEnumerable<string> array)
    {
        var output = new List<CompositionFile>();
        foreach (var location in array)
            try
            {
                if (!File.Exists(location) || Directory.Exists(location))
                {
                    Console.WriteLine($"File: \"{location}\" doesn't exist.");
                    continue;
                }

                var data = await File.ReadAllTextAsync(location);
                output.Add(new CompositionFile
                {
                    Location = location,
                    Data = data
                });
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to open file in args: \"{location}\" - Exception: {e}");
                throw;
            }

        return output;
    }

    private static string GenerateProgressbar(double current, long total, int length = 32)
    {
        Span<char> prg = stackalloc char[length];

        var increment = total / length;
        var display = (int)Math.Floor(current / increment);
        display = display > length ? length : display;
        if (display < 0) display = 0;
        for (var i = 0; i < display; i++) prg[i] = FullBlock;

        for (var i = display; i < length; i++) prg[i] = EmptyBlock;

        return prg.ToString();
    }

    private struct CompositionFile
    {
        public string Location;
        public string Data;
    }
}