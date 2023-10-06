using ManagedBass;
using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio;

public class BassBuffer : AudibleBuffer, IDisposable
{
    protected readonly int SampleHandle;
    private readonly SampleInfo SampleInfo;
    public float _volume => _relative_volume * _context.GlobalVolume;
    public float _relative_volume = .5f;
    
    private readonly AudioContext _context;

    public BassBuffer(AudioContext context, AudioData<float> data, int sample_rate, int max_count = 65535)
    {
        var length = data.GetLength();
        var channels = (int) data.ChannelCount;
        _context = context;

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

        var sample = Bass.CreateSample(length * channels * sizeof(float), sample_rate, channels, max_count, BassFlags.Float);
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

    public override void SetVolume(float volume)
    {
        _relative_volume = volume;
        
        SampleInfo.Volume = _volume;
        Bass.SampleSetInfo(SampleHandle, SampleInfo);
    }

    public override void Play(Action? callback_when_finished = null, bool auto_remove = true)
    {
        var channel = Bass.SampleGetChannel(SampleHandle);
        Bass.ChannelPlay(channel);
    }

    public override void Stop()
    {
        Bass.SampleStop(SampleHandle);
    }

    public override long GetTime_Milliseconds()
    {
        var channels = Bass.SampleGetChannels(SampleHandle);
        var channel = channels[0];

        var length = Bass.ChannelGetPosition(channel);
        return (long)(Bass.ChannelBytes2Seconds(channel, length) * 1000f);
    }

    public override void SeekTime_Milliseconds(long milliseconds)
    {
        var channels = Bass.SampleGetChannels(SampleHandle);
        foreach (var channel in channels)
        {
            var position = Bass.ChannelSeconds2Bytes(channel, milliseconds / 1000f);
            Bass.ChannelSetPosition(channel, position);
        }
    }

    public override void Delete()
    {
        Bass.SampleFree(SampleHandle);
    }

    public void Dispose()
    {
        Delete();
        GC.SuppressFinalize(this);
    }

    public override void SetPause(bool state)
    {
        switch (state)
        {
            case false:
            {
                var channels = Bass.SampleGetChannels(SampleHandle);
                foreach (var channel in channels)
                {
                    Bass.ChannelPlay(channel);
                }

                break;
            }
            
            case true:
            {
                var channels = Bass.SampleGetChannels(SampleHandle);
                foreach (var channel in channels)
                {
                    Bass.ChannelPause(channel);
                }

                break;
            }
        }
    }
}