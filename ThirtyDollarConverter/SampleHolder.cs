using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarEncoder.Wave;
using ThirtyDollarParser;

namespace ThirtyDollarConverter;

public class SampleHolder
{
    public readonly Dictionary<Sound, PcmDataHolder> SampleList = new();
    public Action<string, int, int>? DownloadUpdate = null;

    public const string ThirtyDollarWebsiteUrl = "https://thirtydollar.website";
    public const string DownloadSampleUrl = "https://thirtydollar.website/sounds";
    public string DownloadLocation { get; init; } = "./Sounds";
    public string ImagesLocation => $"{DownloadLocation}/Images";

    public static readonly string[] ActionsArray =
    {
        "action_bg.png",
        "action_combine.png",
        "action_cut.png",
        "action_divider.png",
        "action_flash.png",
        "action_jump.png",
        "action_loop.png",
        "action_loopmany.png",
        "action_looptarget.png",
        "action_pulse.png",
        "action_speed.png",
        "action_startpos.png",
        "action_stop.png",
        "action_target.png",
        "action_transpose.png",
        "action_volume.png"
    };
    /// <summary>
    /// Loads all Thirty Dollar Website sounds to this object.
    /// </summary>
    /// <exception cref="InvalidProgramException">Exception thrown when the application is offline and the sounds.json file doens't exist.</exception>
    /// <exception cref="Exception">Exception thrown when there's an error reading the sounds list.</exception>
    public async Task LoadSampleList()
    {
        var sample_list_location = $"{DownloadLocation}/sounds.json";
        SampleList.Clear();
        PrepareDirectory();
        
        Console.WriteLine("Loading sounds.json file.");
        var client = new HttpClient();
        try
        {
            var response = await client.GetStreamAsync($"{ThirtyDollarWebsiteUrl}/sounds.json");

            await using var download_file_stream = File.Open(sample_list_location, FileMode.Create,
                FileAccess.ReadWrite, FileShare.ReadWrite);
            await response.CopyToAsync(download_file_stream);
            await download_file_stream.FlushAsync();
            await download_file_stream.DisposeAsync();
            download_file_stream.Close();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Trying to reach the Thirty Dollar Website failed with error \'{e}\'. Trying to use the cache instead.");
            if (!File.Exists(sample_list_location)) 
                throw new InvalidProgramException("Cache file \'sounds.json\' not found.");
        }

        await using var open_file_stream = File.Open(sample_list_location, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        var sounds = await JsonSerializer.DeserializeAsync<Sound[]>(open_file_stream);
        
        if (sounds == null)
            throw new Exception("Loading Thirty Dollar Website Sounds failed with error: \'Deserialized contents of sounds.json are empty.\'");

        foreach (var sound in sounds) SampleList.Add(sound, new PcmDataHolder());
    }

    /// <summary>
    /// Checks if the output directory exists and if not creates it, then checks if all samples have been downloaded.
    /// </summary>
    /// <returns>Whether all samples have been downloaded.</returns>
    public void PrepareDirectory()
    {
        if (Directory.Exists(DownloadLocation)) return;

        Directory.CreateDirectory(DownloadLocation);
        Directory.CreateDirectory(ImagesLocation);
    }

    /// <summary>
    /// Downloads all sounds to the download folder.
    /// </summary>
    public async Task DownloadSamples()
    {
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
            await using var fs = File.Open($"{DownloadLocation}/{file}.wav", FileMode.Create);
            await stream.CopyToAsync(fs);
            fs.Close();
            i++;
        }
    }

    public async Task DownloadImages()
    {
        var client = new HttpClient();
        if (SampleList.Count < 1)
        {
            await LoadSampleList();
        }

        PrepareDirectory();
        await DownloadSamples();

        foreach (var (sound, _) in SampleList)
        {
            var filename = sound.Filename;
            const string file_extension = "png";

            var download_location = $"{ImagesLocation}/{filename}.{file_extension}";

            if (File.Exists(download_location)) continue;
            
            Console.WriteLine($"Downloading image: \'{sound.Icon_URL}\'");
            
            var stream = await client.GetStreamAsync(sound.Icon_URL);
            await using var fs = File.Open(download_location, FileMode.CreateNew);
            await stream.CopyToAsync(fs);
            
            fs.Close();
        }

        foreach (var action in ActionsArray)
        {
            var file_name = $"{action}";
            var download_location = $"{ImagesLocation}/{file_name}";
            
            if (File.Exists(download_location)) continue;
            
            Console.WriteLine($"Downloading image: \'{ThirtyDollarWebsiteUrl}/assets/{file_name}\'");
            
            await using var stream = await client.GetStreamAsync($"{ThirtyDollarWebsiteUrl}/assets/{file_name}");
            await using var fs = File.Open(download_location, FileMode.CreateNew);
            await stream.CopyToAsync(fs);
            
            fs.Close();
        }
    }
    
    /// <summary>
    /// Loads all sounds into memory.
    /// </summary>
    /// <exception cref="Exception">Exception that's thrown when a sound is empty.</exception>
    public void LoadSamplesIntoMemory()
    {
        foreach (var (key, _) in SampleList)
        {
            var fileStream = File.OpenRead($"{DownloadLocation}/{key.Id}.wav");
            var decoder = new WaveDecoder();
            Console.WriteLine($"Reading sample: {key.Filename}.wav");
            SampleList[key] = decoder.Read(fileStream);
            SampleList[key].ReadAsFloat32Array(true);

            if (SampleList[key].FloatData?.GetChannel(0).Length == 0)
            {
                throw new Exception($"Sample \'{key.Filename}.wav\' is empty.");
            }
        }

        Console.WriteLine($"Samples: {SampleList.Count}");
    }
}