using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;

namespace ThirtyDollarWebsiteConverter
{
    internal static class Program
    {
        private static double DownloadPercent { get; set; }
        private static string? DownloadFile { get; set; }

        private const char EmptyBlock = '□', FullBlock = '■';

        public static readonly List<short[]> Samples = new();

        private static async Task Main(string[] args)
        {
            if (!HasFiles()) await DownloadFiles();
            await LoadSamplesIntoMemory();
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

            var num = 1;
            foreach (var encoder in output.Select(Composition.FromString).Select(comp => new PcmEncoder
            {
                Composition = comp
            }))
            {
                encoder.Start();
                encoder.Play(num);
                num++;
            }
            
            Console.WriteLine("Finished Executing.");
        }

        private static bool HasFiles()
        {
            if (!Directory.Exists("./Sounds"))
            {
                Directory.CreateDirectory("./Sounds");
                return false;
            }

            var read = Directory.GetFiles("./Sounds");
            return read.Length >= 155;
        }

        private static async Task DownloadFiles()
        {
            var timer = new Timer {Interval = 1 /* Miliseconds */};
            timer.Elapsed += (_, _) =>
            {
                Console.Clear();
                Console.WriteLine("Downloading Items:\n" +
                                  $"({GenerateProgressbar(DownloadPercent, 100, 40)}) {DownloadPercent:F}% - {DownloadFile}");
            };
            timer.Start();
            foreach (var file in LongThings.AudioFiles)
            {
                if (file == "last") continue;
                //var requestUrl = $"https://thirtydollar.website/sounds/{file}.wav";
                var requestUrl = $"https://dankest.gq/ThirtyDollarWebsiteSounds/{file}.wav";
                // All the files have different sample rates and channels, so I reencoded them all to 48000Hz - 1 channel.
                DownloadFile = $"./Sounds/{file}.wav";
                if (File.Exists(DownloadFile)) continue;
                //byte[] buffer = new byte[1024];
                var client = new HttpClient();
                await using var stream = await client.GetStreamAsync(requestUrl);
                await using FileStream fs = File.Open($"./Sounds/{file}.wav", FileMode.Create);
                /*double totalReadBytes = 0;
                double totalBytes = client.MaxResponseContentBufferSize;
                while(true)
                {
                    long readBytes = await stream.ReadAsync(buffer);
                    if (readBytes == 0)
                    {
                        break;
                    }
                    totalReadBytes += readBytes;
                    DownloadPercent = (totalBytes - totalReadBytes) / totalBytes * 100;
                    await fs.WriteAsync(buffer);
                }*/ // I wonder why this doesn't work. But hey, at least Stream.CopyTo works!
                await stream.CopyToAsync(fs);
                fs.Close();
            }

            timer.Dispose();
        }

        private static async Task LoadSamplesIntoMemory()
        {
            foreach (var file in LongThings.AudioFiles)
            {
                if (file == "last")
                {
                    Samples.Add(new short[]{0});
                    continue;
                }
                var fileStream = await File.ReadAllBytesAsync($"./Sounds/{file}.wav");
                var offset = 0;
                
                for (var i = 0; i < fileStream.Length; i++)
                {
                    if (fileStream[i] != 0x64 && fileStream[i + 1] != 0x61 && fileStream[i + 2] != 0x74 &&
                        fileStream[i + 3] != 0x61) continue; // Data Header in Hex Bytes
                    offset = i * 6 + 8;
                    break;
                }

                if (offset == 0) throw new FileLoadException($"Unable to find \"data\" header for file: \"{file}.wav\".");

                var buf = fileStream[offset..];
                
                short[] buffer = new short[buf.Length / 2];
                for (var i = 0; i < buf.Length / 2; i++)
                    //buffer[i] = (short) ((buf[i * 2] & 0xff) | (buf[i * 2 + 1] << 8));
                    buffer[i] = BitConverter.ToInt16(buf, i * 2);
                Samples.Add(buffer);
                Console.WriteLine($"Reading sample: {file}.wav");
            }
            Console.WriteLine($"Samples: {Samples.Count}");
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