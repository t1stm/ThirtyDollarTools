using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarEncoder.Wave;
using ThirtyDollarParser;

namespace ThirtyDollarConverter
{
    public class SampleHolder
    {
        private const string ThirtyDollarWebsiteUrl = "https://thirtydollar.website";
        public string DownloadFile { get; private set; } = "";
        public int DownloadPercent { get; private set; } = 0;
        public readonly Dictionary<Sound, PcmDataHolder> SampleList = new();

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
                SampleList.Add(sound, new PcmDataHolder());
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
                //var requestUrl = $"{ThirtyDollarWebsiteUrl}/sounds/{file}.wav";
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

        public void LoadSamplesIntoMemory()
        {
            foreach (var (key, _) in SampleList)
            {
                var fileStream = File.OpenRead($"./Sounds/{key.Id}.wav");
                var decoder = new WaveDecoder();
                Console.WriteLine($"Reading sample: {key.Filename}.wav");
                SampleList[key] = decoder.Read(fileStream);
            }

            Console.WriteLine($"Samples: {SampleList.Count}");
        }
    }
}