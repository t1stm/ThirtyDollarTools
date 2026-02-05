using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using ReactiveUI;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarGUI.Helpers;
using ThirtyDollarGUI.Views;
using ThirtyDollarParser;

namespace ThirtyDollarGUI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly EncoderSettings _encoderSettings = new()
    {
        Channels = 2,
        SampleRate = 48000
    };

    private readonly SampleHolder _sampleHolder;
    private PcmEncoder? _encoder;

    private bool _encodeRunning;

    private string? _exportFileLocation = "";
    private int _progressBarValue;
    private string? _sequenceFileLocations = "";
    private Sequence[]? _sequences;
    public bool IsExportLocationGood = true;

    public bool IsSequenceLocationGood = true;

    public MainWindowViewModel()
    {
        _sampleHolder = new SampleHolder();
        CheckSampleAvailability();
    }

    public string? SequenceFileLocation
    {
        get => _sequenceFileLocations;
        set
        {
            this.RaiseAndSetIfChanged(ref _sequenceFileLocations, value);
            IsSequenceLocationGood = this.RaiseAndSetIfChanged(ref IsSequenceLocationGood,
                !string.IsNullOrEmpty(value) && File.Exists(value));
            new Task(ReadSequence).Start();
            if (string.IsNullOrEmpty(ExportFileLocation) && IsSequenceLocationGood) ExportFileLocation = value + ".wav";
        }
    }

    public string? ExportFileLocation
    {
        get => _exportFileLocation;
        set
        {
            this.RaiseAndSetIfChanged(ref _exportFileLocation, value);
            IsExportLocationGood = this.RaiseAndSetIfChanged(ref IsExportLocationGood, !string.IsNullOrEmpty(value));
        }
    }

    public int ProgressBarValue
    {
        get => _progressBarValue;
        set => this.RaiseAndSetIfChanged(ref _progressBarValue, value);
    }

    public ObservableCollection<string> Logs { get; } = [];

    public async void Select_SequenceFileLocation()
    {
        var file_picker_types = new[]
        {
            new FilePickerFileType("Thirty Dollar Website Sequence")
            {
                MimeTypes = ["text/plain"],
                Patterns = ["*.🗿"]
            },
            new FilePickerFileType("Any File")
            {
                Patterns = ["*.*"]
            }
        };
        var open_file_dialog = await this.OpenFileDialogAsync("Select sequence files.", file_picker_types);
        if (open_file_dialog == null) return;

        var selected_files = open_file_dialog.ToArray();
        SequenceFileLocation = selected_files.Length switch
        {
            > 1 => string.Join(Environment.NewLine, selected_files),
            1 => selected_files[0],
            _ => SequenceFileLocation
        };
    }

    public async void Select_ExportFileLocation()
    {
        var file_picker_types = new[]
        {
            new FilePickerFileType("RIFF WAVE File")
            {
                MimeTypes = ["audio/wav"],
                Patterns = ["*.wav"]
            }
        };
        var save_file_dialog = await this.SaveFileDialogAsync("Select export location.", file_picker_types);
        ExportFileLocation = save_file_dialog;
    }


    private async void CheckSampleAvailability()
    {
        await _sampleHolder.LoadSampleList();
        _sampleHolder.PrepareDirectory();
        if (!await _sampleHolder.DownloadSamples(true))
        {
            DownloadSamples();
            return;
        }

        // hack that fixes a ghost element
        Logs.Clear();

        CreateLog("All sounds are downloaded. Loading into memory.");
        _sampleHolder.LoadSamplesIntoMemory();
        CreateLog("Loaded all samples into memory.");
    }

    private void DownloadSamples()
    {
        var sample_downloader = new Downloader();
        var downloader_view_model = new DownloaderViewModel(_sampleHolder)
        {
            OnFinishDownloading = sample_downloader.Close
        };

        sample_downloader.DataContext = downloader_view_model;
        sample_downloader.Show();
    }

    private void CreateLog(string message)
    {
        var current_time = DateTime.Now;
        var line = $"[{current_time:HH:mm:ss}] {message}";
        Console.WriteLine(line);

        lock (Logs)
        {
            Logs.Add(line);
        }
    }

    private async void ReadSequence()
    {
        try
        {
            if (_sequenceFileLocations == null) return;
            var sequence_locations = _sequenceFileLocations.Split(Environment.NewLine);
            var new_sequences = new List<Sequence>();
            foreach (var sequence_location in sequence_locations)
            {
                if (!File.Exists(sequence_location)) return;

                var read = await File.ReadAllTextAsync(sequence_location);
                var sequence = Sequence.FromString(read);
                new_sequences.Add(sequence);

                CreateLog($"Preloaded sequence located in: \'{sequence_location}\'");
            }

            _sequences = new_sequences.ToArray();
        }
        catch (Exception e)
        {
            this.RaiseAndSetIfChanged(ref IsSequenceLocationGood, false);
            CreateLog($"Error when reading sequence: \'{e}\'");
        }
    }

    private void UpdateProgressBar(ulong current, ulong max)
    {
        var percent = Math.Ceiling(100f * ((float)current / max));

        var integer = (int)percent;
        ProgressBarValue = integer;
    }

    public async void StartEncoder()
    {
        if (_sequences == null)
        {
            CreateLog("No sequences are selected.");
            return;
        }

        if (!IsExportLocationGood || _exportFileLocation == null)
        {
            CreateLog("The selected export location is bad. Please select a new one.");
            return;
        }

        if (string.IsNullOrEmpty(ExportFileLocation) && IsSequenceLocationGood)
            ExportFileLocation = SequenceFileLocation + ".wav";

        if (_sequenceFileLocations == _exportFileLocation)
        {
            CreateLog(
                "The export file location is the same as the sequence's. Adding .wav at the end of the export location.");
            ExportFileLocation += ".wav";
        }

        if (_encodeRunning)
        {
            CreateLog("An encode is currently running.");
            return;
        }

        _encodeRunning = true;

        CreateLog("Started encoding.");
        _encoder = new PcmEncoder(_sampleHolder, _encoderSettings, CreateLog, UpdateProgressBar);

        await Task.Run(async () => { await EncoderStart(_encoder, _sequences); });
    }

    public void ExportSettings()
    {
        var export_settings = new ExportSettings
        {
            DataContext = new ExportSettingsViewModel(_encoderSettings)
        };

        export_settings.Show();
    }

    private async Task EncoderStart(PcmEncoder pcmEncoder, IEnumerable<Sequence> localSequences)
    {
        var output = await pcmEncoder.GetMultipleSequencesAudio(localSequences);
        CreateLog("Finished encoding.");

        pcmEncoder.WriteAsWavFile(_exportFileLocation ?? throw new Exception("Export path is null."), output);
        _encodeRunning = false;
    }

    public void PreviewSequence()
    {
        var downloader = new Downloader();
        var data_context = new DownloaderViewModel(_sampleHolder, DownloaderMode.Images)
        {
            OnFinishDownloading = DownloadContinue
        };

        downloader.DataContext = data_context;

        if (!Directory.Exists(_sampleHolder.ImagesLocation) ||
            Directory.GetFiles(_sampleHolder.ImagesLocation).Length <=
            _sampleHolder.SampleList.Count + SampleHolder.ActionsArray.Length)
        {
            downloader.Show();
            return;
        }

        DownloadContinue();
        return;

        void DownloadContinue()
        {
            downloader.Close();

            string? visualizer_filename = null;
            var gui_location = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            gui_location =
                gui_location.Replace(
                    gui_location.Contains("ThirtyDollarConverter.GUI.exe")
                        ? "ThirtyDollarConverter.GUI.exe"
                        : "ThirtyDollarConverter.GUI", "");

            if (File.Exists($"{gui_location}/ThirtyDollarVisualizer"))
                visualizer_filename = $"{gui_location}/ThirtyDollarVisualizer";
            if (File.Exists($"{gui_location}/ThirtyDollarVisualizer.exe"))
                visualizer_filename = $"{gui_location}/ThirtyDollarVisualizer.exe";

            if (visualizer_filename == null)
            {
                CreateLog(
                    $"Unable to find the visualizer executable in the directory of the GUI. Location: \'{gui_location}\'");
                return;
            }

            var start_info = new ProcessStartInfo
            {
                FileName = visualizer_filename,
                Arguments = $"-i \"{_sequenceFileLocations}\""
            };

            Process.Start(start_info);
        }
    }
}