using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarEncoder.Wave;
using ThirtyDollarParser;

namespace ThirtyDollarConverter
{
    public class SampleHolder
    {
        public string ThirtyDollarWebsiteUrl { private get; init; } = "https://thirtydollar.website";
        //public string DownloadSampleUrl { private get; init; } = "https://dankest.gq/ThirtyDollarWebsiteSounds";
        public string DownloadSampleUrl { private get; init; } = "https://thirtydollar.website/sounds";
        public string DownloadLocation { private get; init; } = "./Sounds";

        public Action<string, int, int>? DownloadUpdate = null;

        public readonly Dictionary<Sound, PcmDataHolder> SampleList = new();

        public async Task LoadSampleList()
        {
            // TODO: Add error or implement solution when offline.
            DownloadedAllFiles();
            Console.WriteLine("Downloading sounds.json file.");
            var client = new HttpClient();
            var response = await client.GetByteArrayAsync($"{ThirtyDollarWebsiteUrl}/sounds.json");
            var dll = $"{DownloadLocation}/sounds.json";
            await using var fileStream = new FileStream(dll, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            await fileStream.ReadAsync(response);
            await fileStream.FlushAsync();
            await fileStream.DisposeAsync();
            fileStream.Close();
            var sounds = JsonSerializer.Deserialize<Sound[]>(response);
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
                DownloadUpdate?.Invoke(sound.Filename ?? "Empty filename.doesnt_exist", i, count);
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
                SampleList[key].ReadAsFloat32Array(true);
            }

            Console.WriteLine($"Samples: {SampleList.Count}");
        }
    }
}