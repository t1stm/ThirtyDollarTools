using System;
using System.Collections.Generic;
using System.Linq;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Resamplers;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarParser;

namespace ThirtyDollarConverter;

public class SampleProcessor
{
    private readonly Dictionary<Sound, PcmDataHolder> Samples;
    private readonly Action<string> Log;
    private readonly EncoderSettings Settings;
    private IResampler Resampler { get; }

    public SampleProcessor(Dictionary<Sound, PcmDataHolder> sample_holder, EncoderSettings settings, Action<string>? logger = null)
    {
        Samples = sample_holder;
        Settings = settings;
        Resampler = settings.Resampler;
        Log = logger ?? new Action<string>(_ => { });
    }
    
    public AudioData<float> ProcessEvent(Event ev)
    {
        try
        {
            var (_, value) = Samples.AsParallel()
                .FirstOrDefault(pair => pair.Key.Filename == ev.SoundEvent || pair.Key.Id == ev.SoundEvent);
            if (value == null) throw new Exception($"Sound Event: \'{ev.SoundEvent}\' is null.");
            var sampleData = value.ReadAsFloat32Array(Settings.Channels > 1);
            if (sampleData == null)
                throw new NullReferenceException(
                    $"Sample data is null for event: \"{ev}\", Samples Count is: {Samples.Count}");

            var audioData = new AudioData<float>(Settings.Channels);

            for (var i = 0; i < Settings.Channels; i++)
                audioData.Samples[i] = Resampler.Resample(sampleData.GetChannel(i), value.SampleRate,
                    (uint)(Settings.SampleRate / Math.Pow(2, ev.Value / 12)));

            return audioData;
        }
        catch (Exception e)
        {
            Log($"Processing failed: \"{e}\"");
        }

        return AudioData<float>.Empty(Settings.Channels);
    }
}