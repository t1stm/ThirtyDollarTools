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
        public string ThirtyDollarWebsiteUrl { private get; init; } = "https://thirtydollar.website";
        public string DownloadSampleUrl { private get; init; } = "https://dankest.gq/ThirtyDollarWebsiteSounds";
        public string DownloadLocation { private get; init; } = "./Sounds";

        public Action<string, int, int>? DownloadUpdate = null;

        public readonly Dictionary<Sound, PcmDataHolder> SampleList = new();

        public async Task LoadSampleList()
        {
            // TODO: Add error or implement solution when offline.
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
        
        public bool DownloadedAllFiles()
        {
            if (!Directory.Exists(DownloadLocation))
            {
                Directory.CreateDirectory(DownloadLocation);
                return false;
            }

            var read = Directory.GetFiles(DownloadLocation);
            return read.Length >= SampleList.Count;
        }
        
        public async Task DownloadFiles()
        {
            if (DownloadedAllFiles()) return;
            var client = new HttpClient();
            var i = 0;
            var count = SampleList.Count;
            foreach (var (sound, _) in SampleList)
            {
                var file = sound.Id;
                var requestUrl = $"{DownloadSampleUrl}/{file}.wav";
                var dll = $"{DownloadLocation}/{file}.wav";
                if (File.Exists(dll)) continue;
                DownloadUpdate?.Invoke(sound?.Filename ?? "Empty filename.doesnt_exist", i, count);
                await using var stream = await client.GetStreamAsync(requestUrl);
                await using FileStream fs = File.Open($"{DownloadLocation}/{file}.wav", FileMode.Create);
                await stream.CopyToAsync(fs);
                fs.Close();
                i++;
            }
        }

        public void LoadSamplesIntoMemory()
        {
            foreach (var (key, _) in SampleList)
            {
                var fileStream = File.OpenRead($"{DownloadLocation}/{key.Id}.wav");
                var decoder = new WaveDecoder();
                Console.WriteLine($"Reading sample: {key.Filename}.wav");
                SampleList[key] = decoder.Read(fileStream);
            }

            Console.WriteLine($"Samples: {SampleList.Count}");
        }
    }
}