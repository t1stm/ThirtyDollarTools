using ManagedBass;
using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio;

public class AudibleBuffer : IDisposable
{
    protected readonly int SampleHandle;
    private readonly SampleInfo SampleInfo;
    public float _volume => _relative_volume;
    public float _relative_volume = .5f;

    public AudibleBuffer(AudioData<float> data, int sample_rate, int max_count = 65535)
    {
        var length = data.GetLength();
        var channels = (int) data.ChannelCount;

        var samples = new float[length * channels];
        var samples_span = samples.AsSpan();
        for (var i = 0; i < length; i++)
        {
            for (var j = 0; j < channels; j++)
            {
                var idx = i * channels + j;
                samples_span[idx] = data.Samples[i % channels][i];
            }
        }

        var sample = Bass.CreateSample(length * channels * sizeof(float), sample_rate, channels, 65535, BassFlags.Float);
        Bass.SampleSetData(sample, samples);
        SampleHandle = sample;
        SampleInfo = new SampleInfo
        {
            Frequency = sample_rate,
            Volume = _volume,
            Flags = BassFlags.Float,
            Length = length * channels * sizeof(float),
            Max = max_count,
            Channels = 2,
            Mode3D = Mode3D.Off
        };
        
        Bass.SampleSetInfo(SampleHandle, SampleInfo);
    }

    public void SetVolume(float volume = 0.5f)
    {
        _relative_volume = volume;
        
        SampleInfo.Volume = _volume;
        Bass.SampleSetInfo(SampleHandle, SampleInfo);
    }

    public void PlaySample(AudioContext context, Action? callback_when_finished = null, bool auto_remove = true)
    {
        var channel = Bass.SampleGetChannel(SampleHandle);
        Bass.ChannelPlay(channel);
    }

    public void Stop()
    {
        Bass.SampleStop(SampleHandle);
    }

    public void Destroy()
    {
        Bass.SampleFree(SampleHandle);
    }

    public void Dispose()
    {
        Destroy();
        GC.SuppressFinalize(this);
    }
}