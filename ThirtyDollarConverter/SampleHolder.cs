using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ThirtyDollarParser;

namespace ThirtyDollarConverter
{
    public class SampleHolder
    {
        private const string ThirtyDollarWebsiteUrl = "https://thirtydollar.website";
        public string DownloadFile { get; private set; } = "";
        public int DownloadPercent { get; private set; } = 0;
        public readonly Dictionary<Sound, short[]?> SampleList = new();

        public async Task LoadSampleList()
        {
            var client = new HttpClient();
            var sounds = await client.GetFromJsonAsync<Sound[]>($"{ThirtyDollarWebsiteUrl}/sounds.json");
            if (sounds == null)
            {
                throw new HttpRequestException("Request to Thirty Dollar Website: Deserialized Sounds.json is null.");
            }

            foreach (var sound in sounds)
            {
                SampleList.Add(sound, null);
            }
        }
        
        private bool HasFiles()
        {
            if (!Directory.Exists("./Sounds"))
            {
                Directory.CreateDirectory("./Sounds");
                return false;
            }

            var read = Directory.GetFiles("./Sounds");
            return read.Length >= SampleList.Count;
        }
        
        public async Task DownloadFiles()
        {
            if (HasFiles()) return;
            var client = new HttpClient();
            foreach (var (sound, _) in SampleList)
            {
                var file = sound.Id;
                //var requestUrl = $"https://thirtydollar.website/sounds/{file}.wav";
                var requestUrl = $"https://dankest.gq/ThirtyDollarWebsiteSounds/{file}.wav";
                // All the files have different sample rates and channels, so I reencoded them all to 48000Hz - 1 channel.
                DownloadFile = $"./Sounds/{file}.wav";
                if (File.Exists(DownloadFile)) continue;
                //byte[] buffer = new byte[1024];
                Console.WriteLine($"Downloading: \"{requestUrl}\"");
                await using var stream = await client.GetStreamAsync(requestUrl);
                await using FileStream fs = File.Open($"./Sounds/{file}.wav", FileMode.Create);
                await stream.CopyToAsync(fs);
                fs.Close();
            }
        }

        public async Task LoadSamplesIntoMemory()
        {
            foreach (var file in SampleList)
            {
                var fileStream = await File.ReadAllBytesAsync($"./Sounds/{file.Key.Id}.wav");
                var offset = 0;

                for (var i = 0; i < fileStream.Length; i++)
                {
                    if (fileStream[i] != 0x64 && fileStream[i + 1] != 0x61 && fileStream[i + 2] != 0x74 &&
                        fileStream[i + 3] != 0x61)
                        continue; // Data Header in Hex Bytes
                    offset = i * 6 + 8;
                    break;
                }

                if (offset == 0)
                    throw new FileLoadException($"Unable to find \"data\" header for file: \"{file.Key.Id}.wav\".");

                var buf = fileStream[offset..];

                short[] buffer = new short[buf.Length / 2];
                for (var i = 0; i < buf.Length / 2; i++)
                    //buffer[i] = (short) ((buf[i * 2] & 0xff) | (buf[i * 2 + 1] << 8));
                    buffer[i] = BitConverter.ToInt16(buf, i * 2);
                SampleList[file.Key] = buffer;
                Console.WriteLine($"Reading sample: {file}.wav");
            }

            Console.WriteLine($"Samples: {SampleList.Count}");
        }
    }
}