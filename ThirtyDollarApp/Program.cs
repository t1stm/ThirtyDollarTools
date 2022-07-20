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
        private static SampleHolder Holder { get; } = new();

        private const char EmptyBlock = '□', FullBlock = '■';

        private static async Task Main(string[] args)
        {
            var timer = new System.Timers.Timer {Interval = 1};
            timer.Elapsed += (_, _) =>
            {
                Console.Clear();
                Console.WriteLine($"Downloading File: \"{Holder.DownloadFile}\"\n" +
                                  $"({GenerateProgressbar(Holder.DownloadPercent, 100)}) ({Holder.DownloadPercent}% - 100%)");
            };
            await Holder.DownloadFiles();
            await Holder.LoadSamplesIntoMemory();
            Console.Clear();

            var isInBinFolder = Directory.GetCurrentDirectory() == "bin";
            var list = new List<string>
            {
                $"{(isInBinFolder ? "../../.." : ".")}/Included Sequences/(Domburg) bad apple full.🗿",
                $"{(isInBinFolder ? "../../.." : ".")}/Included Sequences/(Radiotomatosauce99) big shot [Deltarune].🗿",
                $"{(isInBinFolder ? "../../.." : ".")}/Included Sequences/(Radiotomatosauce99) It has to be this way [Metal Gear Rising Revengeance].🗿",
                $"{(isInBinFolder ? "../../.." : ".")}/Included Sequences/(Radiotomatosauce99) watery graves [Plants vs. Zombies].🗿",
                $"{(isInBinFolder ? "../../.." : ".")}/Included Sequences/(Xenon Neko) catastrophe_tdw_v2.🗿",
                $"{(isInBinFolder ? "../../.." : ".")}/Included Sequences/(K0KINNIE) 30 dollar bullet hell.🗿"
            };
            var output = new List<string>();
            foreach (var arg in args)
                try
                {
                    if (!File.Exists(arg))
                    {
                        Console.WriteLine($"File: \"{arg}\" doesn't exist.");
                        continue;
                    }

                    var file = await File.ReadAllTextAsync(arg);
                    output.Add(file);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to open file in args: \"{arg}\" - Exception: {e}");
                    throw;
                }

            foreach (var arg in list)
                try
                {
                    if (!File.Exists(arg))
                    {
                        Console.WriteLine($"File: \"{arg}\" doesn't exist.");
                        continue;
                    }

                    var file = await File.ReadAllTextAsync(arg);
                    output.Add(file);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to open file in predefined list: \"{arg}\" - Exception: {e}");
                    throw;
                }

            var num = 0;
            foreach (var encoder in output.Select(Composition.FromString).Select(comp => new PcmEncoder(Holder, comp)))
            {
                encoder.Start();
                encoder.WriteAsWavFile($"./{list[num].Split('/').Last()}.wav");
                num++;
            }

            Console.WriteLine("Finished Executing.");
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