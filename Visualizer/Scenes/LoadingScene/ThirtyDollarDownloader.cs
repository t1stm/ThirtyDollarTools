using LoadingScene.Reports;
using Serilog;
using Shared.Atlases;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Threading;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Parser;

namespace LoadingScene;

public class ThirtyDollarDownloader(ThreadRunner threadRunner, AssetProvider assetProvider)
{
    private readonly ILogger _logger = threadRunner.Logger.ForContext<ThirtyDollarDownloader>();
    public required Action<IProgressReport>? StatusUpdate { get; set; }
    public SampleHolder SampleHolder { get; set; } = new(threadRunner.Logger);
    public AtlasStore AtlasStore { get; set; } = new(assetProvider, threadRunner.Logger);

    private Task? _sampleListTask;

    public Action<string>? OnLoadSound { get; set; }
    public bool AssetsLoaded { get; private set; }
    public bool Loading { get; set; }

    /// <summary>True once sounds.json is in - see <see cref="LoadSampleList" />.</summary>
    public bool SampleListLoaded => _sampleListTask is { IsCompleted: true };

    /// <summary>
    ///     Fetches sounds.json, and nothing else. Split out of <see cref="Load" /> because it
    ///     is a single small request that depends on nothing the program is doing: the
    ///     loading screen starts it while it is still building scenes, so the list is
    ///     already in hand by the time the download it feeds is allowed to begin.
    ///     <para>Calling it more than once returns the first call's task rather than refetching.</para>
    /// </summary>
    public Task LoadSampleList()
    {
        // No status of its own: this runs against the scene preloads, and those own the
        // status line while they do. The loading screen says what it is waiting for only
        // if this is still out once they have finished.
        return _sampleListTask ??= threadRunner.RunTask(() =>
        {
            try
            {
                SampleHolder.LoadSampleList().GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                _logger.Error(e, "Loading sounds.json failed. Continuing with cached files if possible.");
            }
        });
    }

    public void Load()
    {
        if (Loading) return;
        Loading = true;

        threadRunner.RunThread(() =>
        {
            LoadTask().GetAwaiter().GetResult();
            Loading = false;
            AssetsLoaded = true;
        });
    }

    private async Task LoadTask()
    {
        // Awaited rather than re-run: the loading screen starts this well before it starts
        // the download, but nothing stops another caller going straight to Load().
        await LoadSampleList();

        var loadedImages = await CheckFilesAndDownload();
        LoadRemainingSoundsToAssetStore(loadedImages, SampleHolder);
    }

    private async Task<HashSet<Sound>> CheckFilesAndDownload()
    {
        SampleHolder.PrepareDirectory();

        var loadedSounds = new HashSet<Sound>();
        var sampleDownloadReport = new SampleDownloadReport
        {
            Message = "Downloading icons"
        };
        SampleHolder.DownloadUpdate = (sound, current, total) =>
        {
            sampleDownloadReport.Percentage = current / (float)total;
            sampleDownloadReport.SoundName = sound.Id;
            var imagePath = sampleDownloadReport.DownloadLocation = $"{SampleHolder.ImagesLocation}/{sound.Id}.png";
            var filename = sound.Filename;
            if (filename.StartsWith("action_"))
                filename = filename.Replace("action_", "!");

            LoadTextureToAtlasStore(imagePath, filename);
            loadedSounds.Add(sound);
            StatusUpdate?.Invoke(sampleDownloadReport);

            _logger.Debug("Downloaded and loaded image {SoundName} to {ImagePath}", filename, imagePath);
        };
        try
        {
            await SampleHolder.DownloadImages();
        }
        catch (Exception e)
        {
            _logger.Error(e, "Downloading images failed. Some images may be missing.");
        }

        sampleDownloadReport.Message = "Downloading sounds";
        SampleHolder.DownloadUpdate = (sound, current, total) =>
        {
            sampleDownloadReport.Percentage = current / (float)total;
            sampleDownloadReport.SoundName = sound.Id;
            sampleDownloadReport.DownloadLocation = $"{SampleHolder.SamplesLocation}/{sound.Id}.wav";

            StatusUpdate?.Invoke(sampleDownloadReport);
            _logger.Debug("Downloaded sound {SoundName} to {SampleLocation}", sound.Id,
                sampleDownloadReport.DownloadLocation);
        };
        try
        {
            await SampleHolder.DownloadSamples();
        }
        catch (Exception e)
        {
            _logger.Error(e, "Downloading sounds failed. Some sounds may be missing.");
        }

        SampleHolder.LoadSamplesIntoMemory();

        return loadedSounds;
    }

    private void LoadRemainingSoundsToAssetStore(HashSet<Sound> loadedImages, SampleHolder sampleHolder)
    {
        var loadingEvent = new SampleLoadingReport
        {
            Message = "Loading icons"
        };

        var index = 0;
        var length = sampleHolder.SampleList.Count;
        foreach (var (sound, _) in sampleHolder.SampleList)
        {
            loadingEvent.Percentage = index / (double)length;
            StatusUpdate?.Invoke(loadingEvent);

            if (loadedImages.Contains(sound))
            {
                index++;
                continue;
            }

            LoadTextureToAtlasStore($"{sampleHolder.ImagesLocation}/{sound.Id}.*", sound.Filename);
            index++;
        }

        foreach (var action in SampleHolder.ActionsArray)
        {
            var soundName = action.Replace("action_", "!");
            if (loadedImages.Any(image => image.Id == soundName)) continue;
            LoadTextureToAtlasStore($"{sampleHolder.ImagesLocation}/{action}.*", soundName);
        }

        LoadTextureToAtlasStore("Assets/Textures/action_icut.png",
            "#icut", StorageLocation.Assembly);
        LoadTextureToAtlasStore("Assets/Textures/action_missing.png", "#missing", StorageLocation.Assembly);
    }

    private void LoadTextureToAtlasStore(string imagePath, string soundName,
        StorageLocation storageLocation = StorageLocation.Disk)
    {
        lock (AtlasStore)
        {
            var atlasStore = AtlasStore;
            atlasStore.LoadImageAtPath(imagePath, soundName, storageLocation);
            OnLoadSound?.Invoke(soundName);
        }
    }
}