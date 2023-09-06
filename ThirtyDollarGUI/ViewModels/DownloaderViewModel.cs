using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ReactiveUI;
using ThirtyDollarConverter;

namespace ThirtyDollarGUI.ViewModels;

public enum DownloaderMode
{
    Samples,
    Images
}

public class DownloaderViewModel : ViewModelBase
{
    private string? _log = $"Logs go here...{Environment.NewLine}";
    private int progress_bar_value;
    private readonly SampleHolder sample_holder;
    private bool download_running;
    public DownloaderMode DownloadMode;
    
    public DownloaderViewModel(SampleHolder sample_holder, DownloaderMode downloadMode = DownloaderMode.Samples)
    {
        this.sample_holder = sample_holder;
        DownloadMode = downloadMode;
    }
    
    public string? Log
    {
        get => _log;
        set => this.RaiseAndSetIfChanged(ref _log, value);
    }

    public int ProgressBarValue
    {
        get => progress_bar_value;
        set => this.RaiseAndSetIfChanged(ref progress_bar_value, value);
    }

    public string DownloadText => DownloadMode switch
    {
        DownloaderMode.Samples => "This is probably the first time this program has been run. " +
                                  "In order to operate, this program needs to download all the samples that the Thirty Dollar Website has. " +
                                  "You can download them using the button below.",
                                  
        DownloaderMode.Images => "This is probably the first time this program has needed to use images. " +
                                  "In order to operate, this program needs to download all the images that samples in the Thirty Dollar Website have. " +
                                  "You can download them using the button below.",
        
        _ => throw new ArgumentOutOfRangeException(nameof(DownloadMode), "how")
    };
    
    private void CreateLog(string message)
    {
        var current_time = DateTime.Now;
        Log += $"[{current_time:HH:mm:ss}] {message}\n";
    }
    
    private void DownloadMessageHandler(string thing, int current, int max)
    {
        CreateLog($"[({current}) - ({max})] Downloading \'{thing}\'.");
        ProgressBarValue = (int) Math.Floor(current * 100f / max);
    }

    private async Task DownloadSamplesTask()
    {
        sample_holder.DownloadUpdate = DownloadMessageHandler;
        await sample_holder.DownloadFiles();
        sample_holder.LoadSamplesIntoMemory();
        
        CreateLog("Loaded all samples into memory.");
    }

    private async Task DownloadImagesTask()
    {
        var current = 0;
        var total_length = sample_holder.SampleList.Count;
        var images_location = $"{sample_holder.DownloadLocation}/Images";
        if (!Directory.Exists(images_location))
        {
            Directory.CreateDirectory(images_location);
        }

        var http_client = new HttpClient();
        
        foreach (var (sound, _) in sample_holder.SampleList)
        {
            var filename = sound.Filename;
            const string file_extension = "png";

            var download_location = $"{images_location}/{filename}.{file_extension}";
            DownloadMessageHandler($"{filename}.{file_extension}", current, total_length);

            if (File.Exists(download_location))
            {
                current++;
                continue;
            }

            var stream = await http_client.GetStreamAsync(sound.Icon_URL);
            await using var fs = File.Open(download_location, FileMode.CreateNew);
            await stream.CopyToAsync(fs);
            
            fs.Close();
            current++;
        }
    }
    
    public async void Download_Button_Handle()
    {
        if (download_running) return;
        
        download_running = true;

        switch (DownloadMode)
        {
            case DownloaderMode.Samples:
                await DownloadSamplesTask();
                break;
            
            case DownloaderMode.Images:
                await DownloadImagesTask();
                break;
        }
        
        download_running = false;
    }
}