using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using ReactiveUI;
using ThirtyDollarConverter.Objects;
using ThirtyDollarGUI.Models;
using ThirtyDollarGUI.Services;

namespace ThirtyDollarGUI.ViewModels;

public partial class ExportSettingsViewModel(EncoderSettings encoderSettings) : ViewModelBase
{
    public ObservableCollection<ResamplerModel> ListItems { get; } = new(ResamplerService.GetItems());
    public PercentageScale[] ScaleItems { get; } = Enum.GetValues<PercentageScale>();

    public uint SampleRate
    {
        get => encoderSettings.SampleRate;
        set => this.RaiseAndSetIfChanged(ref encoderSettings.SampleRate, value);
    }

    public int ChannelsIndex
    {
        get => (int)encoderSettings.Channels - 1;
        set => this.RaiseAndSetIfChanged(ref encoderSettings.Channels, (uint)(value + 1));
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

    public bool EnableNormalization
    {
        get => encoderSettings.EnableNormalization;
        set => this.RaiseAndSetIfChanged(ref encoderSettings.EnableNormalization, value);
    }

    public PercentageScale VolumeScale
    {
        get => encoderSettings.VolumeScale;
        set => this.RaiseAndSetIfChanged(ref encoderSettings.VolumeScale, value);
    }
    
    public PercentageScale PanScale
    {
        get => encoderSettings.PanScale;
        set => this.RaiseAndSetIfChanged(ref encoderSettings.PanScale, value);
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
        get => ListItems.First(m => m.Resampler.GetType() == encoderSettings.Resampler.GetType());
        set
        {
            var resampler = value.Resampler;
            encoderSettings.Resampler = this.RaiseAndSetIfChanged(ref encoderSettings.Resampler, resampler);
        }
    }

    [GeneratedRegex("^[0-9]+$")]
    private static partial Regex NumberRegex();
}