using System;
using System.Collections.Concurrent;
using System.Linq;
using ThirtyDollarConverter.Audio.Resamplers;
using ThirtyDollarConverter.Objects;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarParser;

namespace ThirtyDollarConverter;

public class SampleProcessor
{
    private readonly Action<string> Log;
    private readonly ConcurrentDictionary<Sound, PcmDataHolder> Samples;
    private readonly EncoderSettings Settings;

    /// <summary>
    ///     Creates a helper that helps resample an event to a given octave.
    /// </summary>
    /// <param name="sample_holder">The loaded samples.</param>
    /// <param name="settings">The encoder's settings.</param>
    /// <param name="logger">Action that handles log messages.</param>
    public SampleProcessor(ConcurrentDictionary<Sound, PcmDataHolder> sample_holder, EncoderSettings settings,
        Action<string>? logger = null)
    {
        Samples = sample_holder;
        Settings = settings;
        Resampler = settings.Resampler;
        Log = logger ?? (_ => { });
    }

    private IResampler Resampler { get; }

    /// <summary>
    ///     Resamples a given event.
    /// </summary>
    /// <param name="ev">The event you want to resample.</param>
    /// <returns>The audio data of the resampled event.</returns>
    /// <exception cref="Exception">Exception thrown when the event's PcmDataHolder is null.</exception>
    /// <exception cref="NullReferenceException">Exception thrown when the event's audio data in the holder is null.</exception>
    public AudioData<float> ProcessEvent(BaseEvent ev)
    {
        try
        {
            var (_, value) =
                Samples.FirstOrDefault(pair => pair.Key.Filename == ev.SoundEvent || pair.Key.Id == ev.SoundEvent);
            if (value == null) throw new Exception($"Data for sound event: \'{ev.SoundEvent}\' is null.");
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