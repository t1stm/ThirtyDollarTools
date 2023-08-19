using System;
using System.Threading.Tasks;
using ReactiveUI;
using ThirtyDollarConverter;

namespace ThirtyDollarGUI.ViewModels;

public class DownloaderViewModel : ViewModelBase
{
    private string? _log = "Logs go here...";
    private int progress_bar_value;
    private readonly SampleHolder sample_holder;
    private bool download_running;
    
    public DownloaderViewModel(SampleHolder sample_holder)
    {
        this.sample_holder = sample_holder;
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
    
    private void CreateLog(string message)
    {
        var current_time = DateTime.Now;
        Log += $"[{current_time:HH:mm:ss}] {message}\n";
    }
    
    private void DownloadMessageHandler(string sample, int current, int max)
    {
        CreateLog($"[({current}) - ({max})] Downloading \'{sample}\'.");
        ProgressBarValue = (int) Math.Floor(current * 100f / max);
    }

    private async Task DownloadTask()
    {
        sample_holder.DownloadUpdate = DownloadMessageHandler;
        await sample_holder.DownloadFiles();
        sample_holder.LoadSamplesIntoMemory();
        
        CreateLog("Loaded all samples into memory.");
    }
    
    public async void Download_Button_Handle()
    {
        if (download_running) return;
        
        download_running = true;
        
        await DownloadTask();
        
        download_running = false;
    }
}