using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarGUI.Helper;
using ThirtyDollarGUI.Views;
using ThirtyDollarParser;

namespace ThirtyDollarGUI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string? sequence_file_location = "";
    private string? export_file_location = "";
    private string? log = "";
    private int progress_bar_value;

    private SampleHolder sample_holder;
    private Composition? composition;
    private PcmEncoder? encoder;

    public bool IsSequenceLocationGood = true;
    public bool IsExportLocationGood = true;

    public MainWindowViewModel()
    {
        sample_holder = new SampleHolder();

        var downloader_view_model = new DownloaderViewModel(sample_holder);
        
        var sample_downloader = new SampleDownloader
        {
            DataContext = downloader_view_model
        };
        
        sample_downloader.Show();
    }

    private void DownloadMessageHandler(string sample, int current, int max)
    {
        CreateLog($"[({current}) - ({max})] Downloading \'{sample}\'.");
    }

    private async void DownloadTask()
    {
        sample_holder.DownloadUpdate = DownloadMessageHandler;
        await sample_holder.LoadSampleList();
        await sample_holder.DownloadFiles();
        sample_holder.LoadSamplesIntoMemory();
        
        CreateLog("Loaded all samples into memory.");
    }
    
    public string? SequenceFileLocation
    {
        get => sequence_file_location;
        set
        {
            this.RaiseAndSetIfChanged(ref sequence_file_location, value);
            IsSequenceLocationGood = this.RaiseAndSetIfChanged(ref IsSequenceLocationGood,
                !string.IsNullOrEmpty(value) && File.Exists(value));
            new Task(ReadComposition).Start();
        }
    }

    public string? ExportFileLocation
    {
        get => export_file_location;
        set
        {
            this.RaiseAndSetIfChanged(ref export_file_location, value);
            IsExportLocationGood = this.RaiseAndSetIfChanged(ref IsExportLocationGood, !string.IsNullOrEmpty(value));
        }
    }

    public int ProgressBarValue
    {
        get => progress_bar_value;
        set => this.RaiseAndSetIfChanged(ref progress_bar_value, value);
    }

    public string? Log
    {
        get => log;
        set => this.RaiseAndSetIfChanged(ref log, value);
    }

    public async void Select_CompositionFileLocation()
    {
        var open_file_dialog = await this.OpenFileDialogAsync("Select sequence file.");
        if (open_file_dialog == null) return;

        SequenceFileLocation = open_file_dialog.FirstOrDefault()?.ToString();
    }
    
    public async void Select_ExportFileLocation()
    {
        var save_file_dialog = await this.SaveFileDialogAsync("Select export location.");
        if (save_file_dialog == null) return;

        ExportFileLocation = save_file_dialog.ToString();
    }

    private void CreateLog(string message)
    {
        var current_time = DateTime.Now;
        Log += $"[{current_time:HH:mm:ss}] {message}\n";
    }

    private async void ReadComposition()
    {
        try
        {
            if (sequence_file_location == null) return;
            if (!File.Exists(sequence_file_location)) return;
            
            var read = await File.ReadAllTextAsync(sequence_file_location);
            composition = Composition.FromString(read);
        }
        catch (Exception e)
        {
            this.RaiseAndSetIfChanged(ref IsSequenceLocationGood, false);
            CreateLog($"Error when reading composition: \'{e}\'");
        }
    }

    private void UpdateProgressBar(ulong current, ulong max)
    {
        var percent = Math.Floor(100f * ((float) current / max));

        var integer = (int) percent;
        ProgressBarValue = integer;
    }

    public void StartEncoder()
    {
        if (composition == null)
        {
            CreateLog("No export location selected.");
            return;
        }

        void index_report(ulong current, ulong max)
        {
            UpdateProgressBar(current, max);
        }
        
        var settings = new EncoderSettings();

        encoder = new PcmEncoder(sample_holder, composition, settings, CreateLog, index_report);
        encoder.SampleComposition(composition);
    }

    public void PreviewSequence()
    {
        
    }
}