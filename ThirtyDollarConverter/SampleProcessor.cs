using System;
using System.Collections.Generic;
using System.Linq;
using ThirtyDollarConverter.Objects;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarEncoder.Resamplers;
using ThirtyDollarParser;

namespace ThirtyDollarConverter;

public class SampleProcessor
{
    private readonly Action<string> _log;
    private readonly Dictionary<Sound, PcmDataHolder> _samples;
    private readonly EncoderSettings _settings;

    /// <summary>
    ///     Creates a helper that helps resample an event to a given octave.
    /// </summary>
    /// <param name="sampleHolder">The loaded samples.</param>
    /// <param name="settings">The encoder's settings.</param>
    /// <param name="logger">Action that handles log messages.</param>
    public SampleProcessor(Dictionary<Sound, PcmDataHolder> sampleHolder, EncoderSettings settings,
        Action<string>? logger = null)
    {
        _samples = sampleHolder;
        _settings = settings;
        Resampler = settings.Resampler;
        _log = logger ?? (_ => { });
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
                _samples.FirstOrDefault(pair => pair.Key.Filename == ev.SoundEvent || pair.Key.Id == ev.SoundEvent);
            if (value == null) throw new Exception($"Data for sound event: \'{ev.SoundEvent}\' is null.");
            var sampleData = value.ReadAsFloat32Array(_settings.Channels > 1);
            if (sampleData == null)
                throw new NullReferenceException(
                    $"Sample data is null for event: \"{ev}\", Samples Count is: {_samples.Count}");

            var audioData = new AudioData<float>(_settings.Channels);

            for (var i = 0; i < _settings.Channels; i++)
                audioData.Samples[i] = Resampler.Resample(sampleData.GetChannel(i), value.SampleRate,
                    (uint)(_settings.SampleRate / Math.Pow(2, ev.Value / 12)));

            return audioData;
        }
        catch (Exception e)
        {
            _log($"Processing failed: \"{e}\"");
        }

        return AudioData<float>.Empty(_settings.Channels);
    }
}