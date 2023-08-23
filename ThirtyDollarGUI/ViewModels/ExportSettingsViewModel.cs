using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using ReactiveUI;
using ThirtyDollarConverter.Objects;
using ThirtyDollarGUI.Models;
using ThirtyDollarGUI.Services;

namespace ThirtyDollarGUI.ViewModels;

public partial class ExportSettingsViewModel : ViewModelBase
{
    private readonly EncoderSettings EncoderSettings;
    public ObservableCollection<ResamplerModel> ListItems { get; }
    
    public ExportSettingsViewModel(EncoderSettings encoderSettings)
    {
        EncoderSettings = encoderSettings;
        ListItems = new ObservableCollection<ResamplerModel>(ResamplerService.GetItems());
    }

    public uint SampleRate
    {
        get => EncoderSettings.SampleRate;
        set => this.RaiseAndSetIfChanged(ref EncoderSettings.SampleRate, value);
    }
    
    public uint Channels
    {
        get => EncoderSettings.Channels;
        set => this.RaiseAndSetIfChanged(ref EncoderSettings.Channels, value);
    }
    
    public uint CutDelayMs
    {
        get => EncoderSettings.CutDelayMs;
        set => this.RaiseAndSetIfChanged(ref EncoderSettings.CutDelayMs, value);
    }
    
    [GeneratedRegex("^[0-9]+$")]
    private static partial Regex NumberRegex();

    public string SampleRateText
    {
        get => SampleRate.ToString();
        set
        {
            var regex = NumberRegex();
            if (!regex.IsMatch(value)) return;

            var parsed = uint.Parse(value);
            SampleRate = parsed;
        }
    }
    
    public string ChannelsText
    {
        get => Channels.ToString();
        set
        {
            var regex = NumberRegex();
            if (!regex.IsMatch(value)) return;

            var parsed = uint.Parse(value);
            Channels = parsed;
        }
    }
    
    public string CutDelayText
    {
        get => CutDelayMs.ToString();
        set
        {
            var regex = NumberRegex();
            if (!regex.IsMatch(value)) return;

            var parsed = uint.Parse(value);
            CutDelayMs = parsed;
        }
    }

    public ResamplerModel SelectedExportSettings
    {
        get => new(EncoderSettings.Resampler);
        set
        {
            var resampler = value.Resampler;
            EncoderSettings.Resampler = this.RaiseAndSetIfChanged(ref EncoderSettings.Resampler, resampler);
        }
    }
}