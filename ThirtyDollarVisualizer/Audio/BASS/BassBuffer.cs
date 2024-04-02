using ManagedBass;
using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio;

public class BassBuffer : AudibleBuffer, IDisposable
{
    private readonly List<int> _active_channels = new();

    private readonly AudioContext _context;
    protected readonly int SampleHandle;
    private readonly SampleInfo SampleInfo;
    private float _pan = 0.5f;
    public float _relative_volume = .5f;

    public unsafe BassBuffer(AudioContext context, AudioData<float> data, int sample_rate, int max_count = 65535)
    {
        var length = data.GetLength();
        var channels = (int)data.ChannelCount;
        _context = context;

        Span<float> samples = new float[length * channels];
        for (var i = 0; i < length; i++)
        for (var j = 0; j < channels; j++)
        {
            var idx = i * channels + j;
            samples[idx] = data.Samples[i % channels][i];
        }

        var sample = Bass.CreateSample(length * channels * sizeof(float), sample_rate, channels, max_count,
            BassFlags.Float);
        fixed (void* s = samples)
        {
            Bass.SampleSetData(sample, new IntPtr(s));
        }

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

    public float _volume => _relative_volume * _context.GlobalVolume;

    public void Dispose()
    {
        Delete();
        GC.SuppressFinalize(this);
    }

    public override void SetVolume(float volume)
    {
        _relative_volume = volume;

        SampleInfo.Volume = _volume;
        Bass.SampleSetInfo(SampleHandle, SampleInfo);
    }

    private void HandleBufferOverflow()
    {
        lock (_active_channels)
        {
            var count = _active_channels.Count;
            var divide = Math.Max(1, count / 32);

            var taken = _active_channels.Take(divide).ToArray();
            foreach (var channel in taken)
            {
                Bass.ChannelStop(channel);
                _active_channels.Remove(channel);
            }
        }
    }

    public override void Play(Action? callback_when_finished = null, bool auto_remove = true)
    {
        if (Bass.CPUUsage > 75d)
        {
            Console.WriteLine($"[BASS] CPU usage reached: {Bass.CPUUsage:0.##}% CPU. Cutting old sounds.");
            HandleBufferOverflow();
        }

        if (_volume < 0.01f) return;

        var channel = Bass.SampleGetChannel(SampleHandle);
        if (Math.Abs(_pan - 0.5f) > 0.01f)
            Bass.ChannelSetAttribute(channel, ChannelAttribute.Pan, _pan);
        Bass.ChannelPlay(channel);
        lock (_active_channels)
        {
            _active_channels.Add(channel);
        }

        var byte_length = Bass.ChannelGetLength(channel);
        var seconds = Bass.ChannelBytes2Seconds(channel, byte_length);

        Task.Run(async () =>
        {
            await Task.Delay((int)(seconds * 1000));
            callback_when_finished?.Invoke();
            lock (_active_channels)
            {
                _active_channels.Remove(channel);
            }
        });
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

    public override void SetPan(float pan)
    {
        pan = Math.Max(-1, Math.Min(1, pan));
        _pan = pan;
    }

    public override void SetPause(bool state)
    {
        switch (state)
        {
            case false:
            {
                var channels = Bass.SampleGetChannels(SampleHandle);
                foreach (var channel in channels) Bass.ChannelPlay(channel);

                break;
            }

            case true:
            {
                var channels = Bass.SampleGetChannels(SampleHandle);
                foreach (var channel in channels) Bass.ChannelPause(channel);

                break;
            }
        }
    }

    ~BassBuffer()
    {
        Dispose();
    }
}