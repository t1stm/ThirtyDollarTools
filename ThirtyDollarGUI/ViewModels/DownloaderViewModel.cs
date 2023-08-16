using System;
using ReactiveUI;
using ThirtyDollarConverter;

namespace ThirtyDollarGUI.ViewModels;

public class DownloaderViewModel : ViewModelBase
{
    private string? _log;
    private int progress_bar_value;
    private SampleHolder SampleHolder;
    
    public DownloaderViewModel(SampleHolder sample_holder)
    {
        SampleHolder = sample_holder;
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
    
    private void DownloadMessageHandler(string sample, int current, int max)
    {
        CreateLog($"[({current}) - ({max})] Downloading \'{sample}\'.");
    }
    
    private void CreateLog(string message)
    {
        var current_time = DateTime.Now;
        Log += $"[{current_time:HH:mm:ss}] {message}\n";
    }
    
    
}