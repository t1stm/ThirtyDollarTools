using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
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
    private string? log = $"Logs go here...{Environment.NewLine}";
    private int progress_bar_value;

    private readonly SampleHolder sample_holder;
    private Sequence? sequence;
    private PcmEncoder? encoder;
    private EncoderSettings encoder_settings = new()
    {
        Channels = 2,
        SampleRate = 48000
    };
    
    private bool encode_running;

    public bool IsSequenceLocationGood = true;
    public bool IsExportLocationGood = true;

    public MainWindowViewModel()
    {
        sample_holder = new SampleHolder();
        CheckSampleAvailability();
    }
    
    public string? SequenceFileLocation
    {
        get => sequence_file_location;
        set
        {
            this.RaiseAndSetIfChanged(ref sequence_file_location, value);
            IsSequenceLocationGood = this.RaiseAndSetIfChanged(ref IsSequenceLocationGood,
                !string.IsNullOrEmpty(value) && File.Exists(value));
            new Task(ReadSequence).Start();
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

    public async void Select_SequenceFileLocation()
    {
        var file_picker_types = new[] { 
            new FilePickerFileType("Thirty Dollar Website Sequence")
            {
                MimeTypes = new [] {"text/plain"},
                Patterns = new [] {"*.🗿"}
            },
            new FilePickerFileType("Any File")
            {
                Patterns = new [] {"*.*"}
            }
        };
        var open_file_dialog = await this.OpenFileDialogAsync("Select sequence file.", file_picker_types);
        if (open_file_dialog == null) return;

        SequenceFileLocation = open_file_dialog.FirstOrDefault();
    }
    
    public async void Select_ExportFileLocation()
    {
        var file_picker_types = new[] { new FilePickerFileType("RIFF WAVE File")
        {
            MimeTypes = new [] {"audio/wav"},
            Patterns = new [] {"*.wav"}
        } };
        var save_file_dialog = await this.SaveFileDialogAsync("Select export location.", file_picker_types);
        ExportFileLocation = save_file_dialog;
    }
    
    
    private async void CheckSampleAvailability()
    {
        await sample_holder.LoadSampleList();
        sample_holder.PrepareDirectory();
        DownloadSamples();

        if (!await sample_holder.DownloadSamples(true)) return; 
        
        sample_holder.LoadSamplesIntoMemory();
        CreateLog("Loaded all samples into memory.");
    }

    private void DownloadSamples()
    {
        var downloader_view_model = new DownloaderViewModel(sample_holder);
        var sample_downloader = new Downloader
        {
            DataContext = downloader_view_model
        };
        
        sample_downloader.Show();
    }

    private void CreateLog(string message)
    {
        var current_time = DateTime.Now;
        Log ??= "";
        Log += $"[{current_time:HH:mm:ss}] {message}\n";
        if (Log.Length > 10000)
            Log = Log[^(Log.Length - 10000)..];
    }

    private async void ReadSequence()
    {
        try
        {
            if (sequence_file_location == null) return;
            if (!File.Exists(sequence_file_location)) return;
            
            var read = await File.ReadAllTextAsync(sequence_file_location);
            sequence = Sequence.FromString(read);
            
            CreateLog($"Preloaded sequence located in: \'{sequence_file_location}\'");
        }
        catch (Exception e)
        {
            this.RaiseAndSetIfChanged(ref IsSequenceLocationGood, false);
            CreateLog($"Error when reading sequence: \'{e}\'");
        }
    }

    private void UpdateProgressBar(ulong current, ulong max)
    {
        var percent = Math.Ceiling(100f * ((float) current / max));

        var integer = (int) percent;
        ProgressBarValue = integer;
    }

    public async void StartEncoder()
    {
        if (sequence == null)
        {
            CreateLog("No export location selected.");
            return;
        }

        if (!IsExportLocationGood || export_file_location == null)
        {
            CreateLog("The selected export location is bad. Please select a new one.");
            return;
        }

        if (encode_running)
        {
            CreateLog("An encode is currently running.");
            return;
        }

        encode_running = true;

        CreateLog("Started encoding.");
        encoder = new PcmEncoder(sample_holder, encoder_settings, CreateLog, UpdateProgressBar);

        await Task.Run(async () =>
        {
            await EncoderStart(encoder, sequence);
        });
    }

    public void ExportSettings()
    {
        var export_settings = new ExportSettings
        {
            DataContext = new ExportSettingsViewModel(encoder_settings)
        };
        
        export_settings.Show();
    }

    private async Task EncoderStart(PcmEncoder pcm_encoder, Sequence localSequence)
    {
        var output = await pcm_encoder.GetSequenceAudio(localSequence);
        CreateLog("Finished encoding.");

        pcm_encoder.WriteAsWavFile(export_file_location ?? throw new Exception("Export path is null."), output);
        encode_running = false;
    }

    public void PreviewSequence()
    {
        var downloader = new Downloader();
        var data_context = new DownloaderViewModel(sample_holder, DownloaderMode.Images)
        {
            OnFinishDownloading = download_continue
        };

        downloader.DataContext = data_context;

        if (!Directory.Exists(sample_holder.ImagesLocation) || 
            Directory.GetFiles(sample_holder.ImagesLocation).Length <= sample_holder.SampleList.Count + SampleHolder.ActionsArray.Length)
        {
            downloader.Show();
            return;
        }

        download_continue();
        return;

        void download_continue()
        {
            downloader.Close();

            string? visualizer_filename = null;
            var gui_location = Process.GetCurrentProcess().MainModule?.FileName;
            if (gui_location.Contains("ThirtyDollarGUI.exe")) gui_location = gui_location.Replace("ThirtyDollarGUI.exe", "");
            else gui_location = gui_location.Replace("ThirtyDollarGUI", "");


            if (File.Exists($"{gui_location}/ThirtyDollarVisualizer")) visualizer_filename = $"{gui_location}/ThirtyDollarVisualizer";
            if (File.Exists($"{gui_location}/ThirtyDollarVisualizer.exe")) visualizer_filename = $"{gui_location}/ThirtyDollarVisualizer.exe";

            if (visualizer_filename == null)
            {
                CreateLog($"Unable to find the visualizer executable in the directory of the GUI. Location: \'{gui_location}\'");
                return;
            }

            var start_info = new ProcessStartInfo
            {
                FileName = visualizer_filename,
                Arguments = $"-i \"{sequence_file_location}\""
            };

            Process.Start(start_info);
        }
    }
}