using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarEncoder.Wave;
using ThirtyDollarParser;

namespace ThirtyDollarConverter;

public class SampleHolder(ILogger logger)
{
    public const string ThirtyDollarWebsiteUrl = "https://thirtydollar.website";
    public const string DownloadSampleUrl = "https://thirtydollar.website/sounds";

    public static readonly string[] ActionsArray =
    [
        "action_bg",
        "action_combine",
        "action_cut",
        "action_divider",
        "action_flash",
        "action_jump",
        "action_loop",
        "action_loopmany",
        "action_looptarget",
        "action_pulse",
        "action_speed",
        "action_startpos",
        "action_stop",
        "action_target",
        "action_transpose",
        "action_volume"
    ];

    private static readonly char Slash = Path.DirectorySeparatorChar;
    private static readonly HttpClient Client = new();
    private readonly ILogger _logger = logger.ForContext<SampleHolder>();

    public Dictionary<Sound, PcmDataHolder> SampleList { get; } = new();
    public Dictionary<string, Sound> StringToSoundReferences { get; } = new();
    public Action<Sound, int, int>? DownloadUpdate { get; set; }

    public string SamplesLocation { get; init; } = $".{Slash}Sounds";
    public string ImagesLocation => $"{SamplesLocation}{Slash}Images";


    /// <summary>
    ///     Loads all Thirty Dollar Website sounds to this object.
    /// </summary>
    /// <exception cref="InvalidProgramException">
    ///     Exception thrown when the application is offline and the sounds.json file
    ///     doesn't exist.
    /// </exception>
    /// <exception cref="Exception">Exception thrown when there's an error reading the sounds list.</exception>
    public async Task LoadSampleList()
    {
        if (SampleList.Count > 0) return;
        var sample_list_location = $"{SamplesLocation}{Slash}sounds.json";
        SampleList.Clear();
        PrepareDirectory();

        _logger.Information("Loading sounds.json file.");
        try
        {
            await using var response = await Client.GetStreamAsync($"{ThirtyDollarWebsiteUrl}/sounds.json");
            await using var download_file_stream = File.Open(sample_list_location, FileMode.Create,
                FileAccess.ReadWrite, FileShare.ReadWrite);

            await response.CopyToAsync(download_file_stream);
            await download_file_stream.FlushAsync();
        }
        catch (Exception e)
        {
            _logger.Error(
                "Trying to reach the Thirty Dollar Website failed with error '{Exception}'. Trying to use the cache instead.",
                e);
            if (!File.Exists(sample_list_location))
                throw new InvalidProgramException("Cache file \'sounds.json\' not found.");
        }

        await using var open_file_stream =
            File.Open(sample_list_location, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        var sounds = await JsonSerializer.DeserializeAsync<Sound[]>(open_file_stream);

        if (sounds == null)
            throw new Exception(
                "Loading Thirty Dollar Website Sounds failed with error: \'Deserialized contents of sounds.json are empty.\'");

        lock (SampleList)
        {
            foreach (var sound in sounds)
            {
                StringToSoundReferences.TryAdd(sound.Id, sound);
                if (sound.Emoji != null)
                    StringToSoundReferences.TryAdd(sound.Emoji, sound);

                SampleList.TryAdd(sound, new PcmDataHolder());
            }
        }
    }

    /// <summary>
    ///     Checks if the output directory exists and if not creates it, then checks if all samples have been downloaded.
    /// </summary>
    /// <returns>Whether all samples have been downloaded.</returns>
    public void PrepareDirectory()
    {
        Directory.CreateDirectory(SamplesLocation);
        Directory.CreateDirectory(ImagesLocation);
    }

    /// <summary>
    ///     Downloads all sounds to the download folder.
    /// </summary>
    public async Task<bool> DownloadSamples(bool checkOnly = false)
    {
        var client = new HttpClient();
        var i = 0;
        var count = SampleList.Count;

        if (checkOnly)
        {
            foreach (var (sound, _) in SampleList)
            {
                var file = sound.Id;
                var dll = $"{SamplesLocation}{Slash}{file}.wav";

                if (File.Exists(dll)) continue;
                if (checkOnly) return false;
            }

            return true;
        }

        await Parallel.ForEachAsync(SampleList, async (pair, token) =>
        {
            var sound = pair.Key;

            var requestUrl = $"{DownloadSampleUrl}/{sound.Id}.wav";
            var dll = $"{SamplesLocation}{Slash}{sound.Id}.wav";

            if (File.Exists(dll)) return;

            {
                await using var httpStream = await client.GetStreamAsync(requestUrl, token);
                await using var fileStream = File.Open(dll, FileMode.Create);

                await httpStream.CopyToAsync(fileStream, token);
                await fileStream.FlushAsync(token);
            }

            DownloadUpdate?.Invoke(sound, i, count);
            i++;
        });

        return true;
    }

    private static bool Exists(string path)
    {
        if (!path.Contains('*'))
            return File.Exists(path);

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory)) directory = Directory.GetCurrentDirectory();

        var searchPattern = Path.GetFileName(path);
        if (string.IsNullOrEmpty(searchPattern))
            throw new ArgumentException("Invalid pattern; no file name specified.", nameof(path));

        var files = Directory.GetFiles(directory, searchPattern);
        return files.Length > 0;
    }

    public async Task DownloadImages()
    {
        var client = new HttpClient();
        if (SampleList.Count == 0) await LoadSampleList();

        PrepareDirectory();
        var i = 0;

        await Parallel.ForEachAsync(SampleList, async (pair, token) =>
        {
            var sound = pair.Key;

            var filename = sound.Id;
            const string downloadExtension = "png";

            var file = $"{ImagesLocation}{Slash}{filename}";
            var download_location = $"{file}.{downloadExtension}";

            if (Exists($"{file}.*")) return;

            {
                var stream = await client.GetStreamAsync(sound.IconUrl, token);
                await using var fs = File.Open(download_location, FileMode.CreateNew);
                await stream.CopyToAsync(fs, token);
                await fs.FlushAsync(token);
            }

            DownloadUpdate?.Invoke(sound, i++, SampleList.Count);
        });

        i = 0;

        await Parallel.ForEachAsync(ActionsArray, async (action, token) =>
        {
            var file_name = $"{action}";
            var download_location = $"{ImagesLocation}{Slash}{file_name}.png";

            if (Exists($"{ImagesLocation}{Slash}{file_name}.*")) return;

            {
                await using var stream =
                    await client.GetStreamAsync($"{ThirtyDollarWebsiteUrl}/assets/{file_name}.png", token);
                await using var fs = File.Open(download_location, FileMode.CreateNew);
                await stream.CopyToAsync(fs, token);
            }

            DownloadUpdate?.Invoke(new Sound
            {
                Id = action
            }, i++, ActionsArray.Length);
        });
    }

    /// <summary>
    ///     Loads all sounds into memory.
    /// </summary>
    /// <exception cref="Exception">Exception that's thrown when a sound is empty.</exception>
    public void LoadSamplesIntoMemory()
    {
        Parallel.ForEach(SampleList, r =>
        {
            var key = r.Key;

            var file_stream = File.OpenRead($"{SamplesLocation}{Slash}{key.Id}.wav");
            var decoder = new WaveDecoder();
            lock (SampleList)
            {
                SampleList[key] = decoder.Read(file_stream);
                SampleList[key].ReadAsFloat32Array(true);
            }
        });

        // creates a hash for all TDW sounds.
        var added_hash_set = SampleList
            .Select(r => r.Key.Filename ?? "")
            .ToHashSet();

        // searches all files again and only adds custom ones.
        Parallel.ForEach(Directory.GetFiles(SamplesLocation), file =>
        {
            var filename = file.Split(Slash).Last();
            if (!filename.EndsWith(".wav")) return;
            var sound = filename.Replace(".wav", "");

            if (added_hash_set.Contains(sound)) return;

            var sound_object = new Sound
            {
                Id = sound,
                Name = sound
            };

            var file_stream = File.OpenRead($"{SamplesLocation}{Slash}{sound}.wav");

            var decoder = new WaveDecoder();
            var holder = decoder.Read(file_stream);
            holder.ReadAsFloat32Array(true);
            lock (SampleList)
            {
                SampleList.Add(sound_object, holder);
            }
        });

        Console.WriteLine($"Samples: {SampleList.Count}");
    }
}