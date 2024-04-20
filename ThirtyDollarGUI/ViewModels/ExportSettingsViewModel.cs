using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using ReactiveUI;
using ThirtyDollarConverter.Objects;
using ThirtyDollarGUI.Models;
using ThirtyDollarGUI.Services;

namespace ThirtyDollarGUI.ViewModels;

public partial class ExportSettingsViewModel(EncoderSettings encoderSettings) : ViewModelBase
{
    public ObservableCollection<ResamplerModel> ListItems { get; } = new(ResamplerService.GetItems());

    public uint SampleRate
    {
        get => encoderSettings.SampleRate;
        set => this.RaiseAndSetIfChanged(ref encoderSettings.SampleRate, value);
    }

    public uint Channels
    {
        get => encoderSettings.Channels;
        set => this.RaiseAndSetIfChanged(ref encoderSettings.Channels, value);
    }

    public uint CutDelayMs
    {
        get => encoderSettings.CutFadeLengthMs;
        set => this.RaiseAndSetIfChanged(ref encoderSettings.CutFadeLengthMs, value);
    }

    public int EncodeSlicesCount
    {
        get => encoderSettings.MultithreadingSlices;
        set => this.RaiseAndSetIfChanged(ref encoderSettings.MultithreadingSlices, value);
    }

    public uint CombineDelayMs
    {
        get => encoderSettings.CombineDelayMs;
        set => this.RaiseAndSetIfChanged(ref encoderSettings.CombineDelayMs, value);
    }

    public bool EnableNormalization
    {
        get => encoderSettings.EnableNormalization;
        set => this.RaiseAndSetIfChanged(ref encoderSettings.EnableNormalization, value);
    }

    public string SampleRateText
    {
        get => SampleRate.ToString();
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                SampleRate = 0;
                return;
            }

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
            if (string.IsNullOrEmpty(value))
            {
                Channels = 0;
                return;
            }

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
            if (string.IsNullOrEmpty(value))
            {
                CutDelayMs = 0;
                return;
            }

            var regex = NumberRegex();
            if (!regex.IsMatch(value)) return;

            var parsed = uint.Parse(value);
            CutDelayMs = parsed;
        }
    }

    public string CombineDelayText
    {
        get => CombineDelayMs.ToString();
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                CombineDelayMs = 0;
                return;
            }

            var regex = NumberRegex();
            if (!regex.IsMatch(value)) return;

            var parsed = uint.Parse(value);
            CombineDelayMs = parsed;
        }
    }

    public string EncodeSlicesCountText
    {
        get => EncodeSlicesCount.ToString();
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                EncodeSlicesCount = 0;
                return;
            }

            var regex = NumberRegex();
            if (!regex.IsMatch(value)) return;

            var parsed = int.Parse(value);
            EncodeSlicesCount = parsed;
        }
    }

    public ResamplerModel SelectedExportSettings
    {
        get => new(encoderSettings.Resampler);
        set
        {
            var resampler = value.Resampler;
            encoderSettings.Resampler = this.RaiseAndSetIfChanged(ref encoderSettings.Resampler, resampler);
        }
    }

    [GeneratedRegex("^[0-9]+$")]
    private static partial Regex NumberRegex();
}