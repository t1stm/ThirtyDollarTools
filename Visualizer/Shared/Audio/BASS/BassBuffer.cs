using System.Buffers;
using System.Runtime.InteropServices;
using ManagedBass;
using Serilog;
using ThirtyDollarEncoder.PCM;

namespace Shared.Audio.BASS;

public class BassBuffer : AudibleBuffer, IDisposable
{
    private readonly int _maxCount;

    private int _sampleRate;

    public BassBuffer(ILogger logger, AudioData<float> data, int sampleRate, int maxCount = 65535)
    {
        var bassLogger = logger.ForContext<BassBuffer>();
        _maxCount = maxCount;

        if (!UploadNewData(data, sampleRate))
            bassLogger.Fatal("Failed to upload new data to BASS");
    }

    private SampleInfo SampleInfo { get; set; } = new();
    protected int SampleHandle { get; set; }
    private float Pan { get; set; } = 0.5f;
    public float Volume { get; set; } = .5f;
    public override bool IsRunning { get; protected set; }

    public void Dispose()
    {
        Delete();
        GC.SuppressFinalize(this);
    }

    public override void SetVolume(float volume)
    {
        Volume = volume;
        SampleInfo.Volume = volume;
        Bass.SampleSetInfo(SampleHandle, SampleInfo);

        var channels = Bass.SampleGetChannels(SampleHandle);
        if (channels == null) return;
        foreach (var channel in channels)
            Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, volume);
    }

    public sealed override unsafe bool UploadNewData(AudioData<float> data, int sampleRate)
    {
        _sampleRate = sampleRate;
        var length = data.GetLength();
        var channels = (int)data.ChannelCount;

        var pool = ArrayPool<byte>.Shared.Rent(length * channels * sizeof(float));
        var samples = MemoryMarshal.Cast<byte, float>(pool.AsSpan());

        for (var i = 0; i < length; i++)
            for (var j = 0; j < channels; j++)
            {
                var idx = i * channels + j;
                samples[idx] = data.Samples[j][i];
            }

        if (SampleHandle != 0)
            Delete();

        SampleHandle = Bass.CreateSample(length * channels * sizeof(float), _sampleRate, channels, _maxCount,
            BassFlags.Float);

        fixed (void* s = samples)
        {
            if (!Bass.SampleSetData(SampleHandle, new IntPtr(s))) return false;
        }

        SampleInfo = new SampleInfo
        {
            Frequency = _sampleRate,
            Volume = Volume,
            Flags = BassFlags.Float,
            Length = length * channels * sizeof(float),
            Max = 65535,
            Channels = 2,
            Mode3D = Mode3D.Off
        };

        Bass.SampleSetInfo(SampleHandle, SampleInfo);
        ArrayPool<byte>.Shared.Return(pool);

        return true;
    }

    public override void Play(Action? callbackWhenFinished = null, bool autoRemove = true)
    {
        var channel = Bass.SampleGetChannel(SampleHandle);
        if (Math.Abs(Pan - 0.5f) > 0.01f)
            Bass.ChannelSetAttribute(channel, ChannelAttribute.Pan, Pan);
        Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, Volume);
        Bass.ChannelPlay(channel);
        IsRunning = true;
    }

    public override void Stop()
    {
        Bass.SampleStop(SampleHandle);
        IsRunning = false;
    }

    public override long GetTime_Milliseconds()
    {
        var channels = Bass.SampleGetChannels(SampleHandle);
        if (channels == null) return 0;
        if (channels.Length < 1) return 0;
        var channel = channels[0];

        var length = Bass.ChannelGetPosition(channel);
        return (long)(Bass.ChannelBytes2Seconds(channel, length) * 1000f);
    }

    public override void SeekTime_Milliseconds(long milliseconds)
    {
        var channels = Bass.SampleGetChannels(SampleHandle);
        if (channels == null || channels.Length == 0)
        {
            var channel = Bass.SampleGetChannel(SampleHandle);
            if (channel == 0) return;
            channels = [channel];
            if (IsRunning) Bass.ChannelPlay(channel);
        }

        foreach (var channel in channels)
        {
            var position = Bass.ChannelSeconds2Bytes(channel, milliseconds / 1000f);
            Bass.ChannelSetPosition(channel, position);
        }
    }

    public override void Delete()
    {
        Bass.SampleStop(SampleHandle);
        Bass.SampleFree(SampleHandle);
        SampleHandle = 0;
    }

    public override void SetPan(float pan)
    {
        pan = Math.Max(-1, Math.Min(1, pan));
        Pan = pan;
    }

    public override void SetPause(bool state)
    {
        switch (state)
        {
            case false:
                {
                    var channels = Bass.SampleGetChannels(SampleHandle);
                    foreach (var channel in channels) Bass.ChannelPlay(channel);
                    IsRunning = true;
                    break;
                }

            case true:
                {
                    var channels = Bass.SampleGetChannels(SampleHandle);
                    foreach (var channel in channels) Bass.ChannelPause(channel);
                    IsRunning = false;
                    break;
                }
        }
    }

    ~BassBuffer()
    {
        Dispose();
    }
}