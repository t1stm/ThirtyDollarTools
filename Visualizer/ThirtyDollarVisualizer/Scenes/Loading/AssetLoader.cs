using ThirtyDollarConverter;
using ThirtyDollarVisualizer.Engine.Asset_Management;
using ThirtyDollarVisualizer.Engine.Threading;
using ThirtyDollarVisualizer.Objects.Playfield.Atlas;
using ThirtyDollarVisualizer.Scenes.Loading.Reports;

namespace ThirtyDollarVisualizer.Scenes.Loading;

public class AssetLoader(ThreadRunner threadRunner, AssetProvider assetProvider)
{
    public required Action<IProgressReport>? StatusUpdate { get; set; }
    public SampleHolder SampleHolder { get; set; } = new();
    public AtlasStore AtlasStore { get; set; } = new(assetProvider);
    
    public bool AssetsLoaded { get; set; } = false;
    public bool Loading { get; set; }

    public void Load()
    {
        if (Loading) return;
        Loading = true;
        
        threadRunner.RunThread(() =>
        {
            LoadTask().GetAwaiter().GetResult();
            Loading = false;
        });
    }

    private async Task LoadTask()
    {
        var loadedImages = await LoadSampleHolder(AtlasStore);
        await LoadRemainingSoundToAssetStore(loadedImages, SampleHolder);
    }

    private async Task<HashSet<string>> LoadSampleHolder(AtlasStore atlasStore)
    {
        StatusUpdate?.Invoke(new LoadingSoundsListReport());
        await SampleHolder.LoadSampleList();
        SampleHolder.PrepareDirectory();

        var loadedSounds = new HashSet<string>();
        var sampleDownloadReport = new SampleDownloadReport
        {
            Message = "Downloading Sample Images..."
        };
        SampleHolder.DownloadUpdate = (soundName, current, total) =>
        {
            sampleDownloadReport.Percentage = current / (float)total;
            sampleDownloadReport.SoundName = soundName;
            var imagePath = sampleDownloadReport.DownloadLocation = $"{SampleHolder.ImagesLocation}/{soundName}.png";
            
            StatusUpdate?.Invoke(sampleDownloadReport);
            LoadTextureToAtlasStore(imagePath, soundName);
            loadedSounds.Add(soundName);
        };
        await SampleHolder.DownloadImages();

        sampleDownloadReport.Message = "Downloading Sample Sounds...";
        SampleHolder.DownloadUpdate = (soundName, current, total) =>
        {
            sampleDownloadReport.Percentage = current / (float)total;
            sampleDownloadReport.SoundName = soundName;
            sampleDownloadReport.DownloadLocation = $"{SampleHolder.SamplesLocation}/{soundName}.png";
            
            StatusUpdate?.Invoke(sampleDownloadReport);
        };
        await SampleHolder.DownloadSamples();
        
        SampleHolder.LoadSamplesIntoMemory();
        return loadedSounds;
    }

    private void LoadTextureToAtlasStore(string imagePath, string soundName)
    {
        lock (AtlasStore)
        {
            var atlasStore = AtlasStore;
            atlasStore.LoadImageAtPath(imagePath, soundName);
        }
    }
    
    private async Task LoadRemainingSoundToAssetStore(HashSet<string> loadedImages, SampleHolder sampleHolder)
    {
        
    }
}