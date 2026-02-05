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

public class DownloaderViewModel(SampleHolder sampleHolder, DownloaderMode downloadMode = DownloaderMode.Samples)
    : ViewModelBase
{
    private bool _downloadRunning;
    private string? _log = $"Logs go here...{Environment.NewLine}";
    private int _progressBarValue;
    public DownloaderMode DownloadMode = downloadMode;
    public Action? OnFinishDownloading { get; init; }

    public string? Log
    {
        get => _log;
        set => this.RaiseAndSetIfChanged(ref _log, value);
    }

    public int ProgressBarValue
    {
        get => _progressBarValue;
        set => this.RaiseAndSetIfChanged(ref _progressBarValue, value);
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
        CreateLog($"[({current}) - ({max})] Downloaded \'{thing}\'.");
        ProgressBarValue = (int)Math.Floor(current * 100f / max);
    }

    private async Task DownloadSamplesTask()
    {
        sampleHolder.DownloadUpdate = DownloadMessageHandler;
        await sampleHolder.DownloadSamples();
        sampleHolder.LoadSamplesIntoMemory();

        CreateLog("Loaded all samples into memory.");
    }

    private async Task DownloadImagesTask()
    {
        var current = 0;
        var total_length = sampleHolder.SampleList.Count + SampleHolder.ActionsArray.Length;

        if (!Directory.Exists(sampleHolder.ImagesLocation)) Directory.CreateDirectory(sampleHolder.ImagesLocation);

        var http_client = new HttpClient();

        foreach (var (sound, _) in sampleHolder.SampleList)
        {
            var filename = sound.Filename;
            const string fileExtension = "png";

            var download_location = $"{sampleHolder.ImagesLocation}/{filename}.{fileExtension}";
            DownloadMessageHandler($"{filename}.{fileExtension}", current, total_length);

            if (File.Exists(download_location))
            {
                current++;
                continue;
            }

            var stream = await http_client.GetStreamAsync(sound.IconUrl);
            await using var fs = File.Open(download_location, FileMode.CreateNew);
            await stream.CopyToAsync(fs);

            fs.Close();
            current++;
        }

        foreach (var action in SampleHolder.ActionsArray)
        {
            var file_name = $"{action}";
            var download_location = $"{sampleHolder.ImagesLocation}/{file_name}";

            DownloadMessageHandler(file_name, current, total_length);
            if (File.Exists(download_location))
            {
                current++;
                continue;
            }

            await using var stream =
                await http_client.GetStreamAsync($"{SampleHolder.ThirtyDollarWebsiteUrl}/assets/{file_name}");
            await using var fs = File.Open(download_location, FileMode.CreateNew);
            await stream.CopyToAsync(fs);

            fs.Close();
            current++;
        }
    }

    public async void Download_Button_Handle()
    {
        if (_downloadRunning) return;

        _downloadRunning = true;

        switch (DownloadMode)
        {
            case DownloaderMode.Samples:
                await DownloadSamplesTask();
                break;

            case DownloaderMode.Images:
                await DownloadImagesTask();
                break;
        }

        _downloadRunning = false;
        OnFinishDownloading?.Invoke();
    }
}