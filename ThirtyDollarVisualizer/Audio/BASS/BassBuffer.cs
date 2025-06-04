using System.Buffers;
using System.Runtime.InteropServices;
using ManagedBass;
using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio.BASS;

public class BassBuffer : AudibleBuffer, IDisposable
{
    private readonly List<int> _activeChannels = [];

    private readonly AudioContext _context;
    private readonly SampleInfo _sampleInfo;
    protected readonly int SampleHandle;
    private float _pan = 0.5f;
    public float RelativeVolume = .5f;

    public unsafe BassBuffer(AudioContext context, AudioData<float> data, int sampleRate, int maxCount = 65535)
    {
        var length = data.GetLength();
        var channels = (int)data.ChannelCount;
        _context = context;

        var pool = ArrayPool<byte>.Shared.Rent(length * channels * sizeof(float));
        var samples = MemoryMarshal.Cast<byte, float>(pool.AsSpan());

        for (var i = 0; i < length; i++)
        for (var j = 0; j < channels; j++)
        {
            var idx = i * channels + j;
            samples[idx] = data.Samples[j][i];
        }

        var sample = Bass.CreateSample(length * channels * sizeof(float), sampleRate, channels, maxCount,
            BassFlags.Float);
        fixed (void* s = samples)
        {
            Bass.SampleSetData(sample, new IntPtr(s));
        }

        SampleHandle = sample;
        _sampleInfo = new SampleInfo
        {
            Frequency = sampleRate,
            Volume = _volume,
            Flags = BassFlags.Float,
            Length = length * channels * sizeof(float),
            Max = maxCount,
            Channels = 2,
            Mode3D = Mode3D.Off
        };

        Bass.SampleSetInfo(SampleHandle, _sampleInfo);
        ArrayPool<byte>.Shared.Return(pool);
    }

    public float _volume => RelativeVolume * _context.GlobalVolume;

    public void Dispose()
    {
        Delete();
        GC.SuppressFinalize(this);
    }

    public override void SetVolume(float volume, bool absolute = false)
    {
        RelativeVolume = volume;

        _sampleInfo.Volume = absolute ? volume / _context.GlobalVolume : _volume;
        Bass.SampleSetInfo(SampleHandle, _sampleInfo);
    }

    private void HandleBufferOverflow()
    {
        lock (_activeChannels)
        {
            var count = _activeChannels.Count;
            var divide = Math.Max(1, count / 32);

            var taken = _activeChannels.Take(divide);
            foreach (var channel in taken)
            {
                Bass.ChannelStop(channel);
                _activeChannels.Remove(channel);
            }
        }
    }

    public override void Play(Action? callbackWhenFinished = null, bool autoRemove = true)
    {
        if (Bass.CPUUsage > 75d)
        {
            Console.WriteLine($"[BASS] CPU usage reached: {Bass.CPUUsage:0.##}% CPU. Cutting old sounds.");
            HandleBufferOverflow();
        }

        if (_volume < 0.001f) return;

        var channel = Bass.SampleGetChannel(SampleHandle);
        if (Math.Abs(_pan - 0.5f) > 0.01f)
            Bass.ChannelSetAttribute(channel, ChannelAttribute.Pan, _pan);
        Bass.ChannelPlay(channel);
        lock (_activeChannels)
        {
            _activeChannels.Add(channel);
        }

        var byte_length = Bass.ChannelGetLength(channel);
        var seconds = Bass.ChannelBytes2Seconds(channel, byte_length);

        Task.Run(async () =>
        {
            await Task.Delay((int)(seconds * 1000));
            callbackWhenFinished?.Invoke();
            lock (_activeChannels)
            {
                _activeChannels.Remove(channel);
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