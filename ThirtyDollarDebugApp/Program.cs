using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ThirtyDollarConverter;
using ThirtyDollarParser;

namespace ThirtyDollarApp
{
    internal static class Program
    {
        private const char EmptyBlock = '□', FullBlock = '■';

        private static async Task Main(string[] args)
        {
            //var isInBinFolder = Directory.GetCurrentDirectory() == "bin";
            //var workingDirectory = $"{(isInBinFolder ? "../../.." : ".")}";
            const string workingDirectory = "/home/kris/RiderProjects/ThirtyDollarWebsiteConverter/ThirtyDollarDebugApp";
            var sequenceDirectory = $"{workingDirectory}/Included Sequences";
            
            var holder = new SampleHolder
            {
                DownloadLocation = $"{workingDirectory}/Sounds"
            };
            await holder.LoadSampleList();
            await holder.DownloadFiles();
            holder.LoadSamplesIntoMemory();
            Console.Clear();
            
            var list = Directory.GetFiles(sequenceDirectory).ToList();

            Directory.CreateDirectory("./Export");

            var output = new List<CompositionFile>();
            output.AddRange(await ReadFileList(args));
            output.AddRange(await ReadFileList(list));

            foreach (var file in output)
            {
                if (file.Location.Contains("LICENSE")) continue;
                var composition = Composition.FromString(file.Data);

                var encoder = new PcmEncoder(holder, composition, new EncoderSettings
                {
                    SampleRate = 48000,
                    Channels = 2
                }, Console.WriteLine);

                var audioData = encoder.SampleComposition(encoder.Composition); // Shame on me...
                encoder.WriteAsWavFile($"./Export/{file.Location.Split('/').Last()}.wav", audioData);
            }

            Console.WriteLine("Finished Executing.");
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

        private struct CompositionFile
        {
            public string Location;
            public string Data;
        }

        private static string GenerateProgressbar(double current, long total, int length = 32)
        {
            Span<char> prg = stackalloc char[length];

            var increment = total / length;
            var display = (int) (current / increment);
            display = display > length ? length : display;
            for (var i = 0; i < display; i++) prg[i] = FullBlock;

            for (var i = display; i < length; i++) prg[i] = EmptyBlock;

            return prg.ToString();
        }
    }
}