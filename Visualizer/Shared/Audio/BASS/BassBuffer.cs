using System.Runtime.InteropServices;
using ManagedBass;
using Serilog;
using ThirtyDollarConverter.Encoder.PCM;

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

    /// <summary>The samples as the source holds them - see <see cref="AudioContext.GetBufferObject(PcmDataHolder)" />.</summary>
    public BassBuffer(ILogger logger, PcmDataHolder pcm, int maxCount = 65535)
    {
        _maxCount = maxCount;
        if (!UploadNative(pcm))
            logger.ForContext<BassBuffer>().Fatal("Failed to upload new data to BASS");
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
        // The sample's own volume is the default for channels created from it later and is a
        // 0-1 setting; amplification rides the channel attribute below, which BASS does allow
        // past 1 (at the cost of clipping, which is the caller's business).
        SampleInfo.Volume = Math.Min(volume, 1f);
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

        // Native scratch, freed as soon as BASS has copied it. A pooled array stays with the
        // pool once returned, and an editor wave reference's is the size of the whole file,
        // uploaded from whichever thread decoded it - every such thread would keep one.
        var samples = (float*)NativeMemory.Alloc((nuint)(length * channels), sizeof(float));
        try
        {
            for (var i = 0; i < length; i++)
            for (var j = 0; j < channels; j++)
                samples[i * channels + j] = data.Samples[j][i];

            if (SampleHandle != 0)
                Delete();

            SampleHandle = Bass.CreateSample(length * channels * sizeof(float), _sampleRate, channels, _maxCount,
                BassFlags.Float);

            if (!Bass.SampleSetData(SampleHandle, (IntPtr)samples)) return false;
        }
        finally
        {
            NativeMemory.Free(samples);
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
        return true;
    }

    private unsafe bool UploadNative(PcmDataHolder pcm)
    {
        _sampleRate = (int)pcm.SampleRate;
        var data = pcm.AudioData ?? [];
        var channels = (int)pcm.Channels;

        if (pcm.Encoding == Encoding.Int24)
        {
            var count = data.Length / 3;
            var widened = (float*)NativeMemory.Alloc((nuint)count, sizeof(float));
            try
            {
                DataHolderExtensions.Int24ToFloat(data, new Span<float>(widened, count));
                return Create(count * sizeof(float), BassFlags.Float, (IntPtr)widened);
            }
            finally
            {
                NativeMemory.Free(widened);
            }
        }

        var format = pcm.Encoding switch
        {
            Encoding.Int8 => BassFlags.Byte, // unsigned, in a WAV and in BASS alike
            Encoding.Int16 => BassFlags.Default,
            Encoding.Float32 => BassFlags.Float,
            _ => throw new InvalidDataException($"{(int)pcm.Encoding}-bit samples aren't supported.")
        };

        fixed (byte* bytes = data)
        {
            return Create(data.Length, format, (IntPtr)bytes);
        }

        bool Create(int length, BassFlags flags, IntPtr samples)
        {
            SampleHandle = Bass.CreateSample(length, _sampleRate, channels, _maxCount, flags);
            if (SampleHandle == 0 || !Bass.SampleSetData(SampleHandle, samples)) return false;

            SampleInfo = Bass.SampleGetInfo(SampleHandle);
            return true;
        }
    }

    public override AudioVoice NewVoice()
    {
        return new BassVoice(Bass.SampleGetChannel(SampleHandle));
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

    /// <summary>
    ///     One channel of the sample. BASS hands a new channel out paused at the start, and one
    ///     that plays off the end of the data stops but stays usable - seek it and play it again.
    ///     Only <see cref="Bass.ChannelStop" /> frees a sample channel.
    /// </summary>
    private sealed class BassVoice(int channel) : AudioVoice
    {
        public override long GetTime_Milliseconds()
        {
            return (long)(Bass.ChannelBytes2Seconds(channel, Bass.ChannelGetPosition(channel)) * 1000);
        }

        public override void SeekTime_Milliseconds(long milliseconds)
        {
            Bass.ChannelSetPosition(channel, Bass.ChannelSeconds2Bytes(channel, milliseconds / 1000d));
        }

        public override void SetPause(bool paused)
        {
            if (paused) Bass.ChannelPause(channel);
            else Bass.ChannelPlay(channel);
        }

        public override void SetVolume(float volume)
        {
            Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, volume);
        }

        public override void Delete()
        {
            Bass.ChannelStop(channel);
        }
    }
}