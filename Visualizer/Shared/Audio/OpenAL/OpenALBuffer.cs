using System.Buffers;
using OpenTK.Audio.OpenAL;
using Serilog;
using ThirtyDollarConverter.Encoder.PCM;

namespace Shared.Audio.OpenAL;

public class OpenALBuffer : AudibleBuffer
{
    private readonly List<int> _audioSources = [];
    private readonly AudioContext _context;

    private readonly ILogger _logger;
    public float Volume = .5f;
    private float _pan;

    public OpenALBuffer(AudioContext context, ILogger logger, AudioData<float> sampleData, int sampleRate)
    {
        _logger = logger.ForContext<OpenALBuffer>();
        var length = sampleData.Samples[0].LongLength;
        var channels = (int)sampleData.ChannelCount;
        _context = context;

        var format = channels switch
        {
            1 => Format.FormatMonoFloat32,
            2 => Format.FormatStereoFloat32,
            _ => throw new ArgumentOutOfRangeException(nameof(sampleData), "The given channels count is invalid.")
        };

        // Interleaved scratch: written in full, handed to OpenAL, which copies it into the buffer
        // object, then dropped. AL.BufferData takes an explicit size, so a rented array being
        // longer than asked for makes no difference to what gets uploaded.
        var samples = ArrayPool<float>.Shared.Rent((int)length * channels);
        try
        {
            var samples_span = samples.AsSpan();
            for (var i = 0; i < length; i++)
            for (var j = 0; j < channels; j++)
            {
                var idx = i * channels + j;
                samples_span[idx] = sampleData.Samples[j][i];
            }

            AudioBuffer = AL.GenBuffer();
            AL.BufferData(AudioBuffer, format, samples, -1, sampleRate);
        }
        finally
        {
            ArrayPool<float>.Shared.Return(samples);
        }
    }

    public int AudioBuffer { get; set; }

    public override bool IsRunning { get; protected set; }

    public override bool UploadNewData(AudioData<float> data, int sampleRate)
    {
        var length = data.GetLength();
        var channels = (int)data.ChannelCount;

        var format = channels switch
        {
            1 => Format.FormatMonoFloat32,
            2 => Format.FormatStereoFloat32,
            _ => throw new ArgumentOutOfRangeException(nameof(data), "The given channels count is invalid.")
        };

        var samples = ArrayPool<float>.Shared.Rent((int)(length * channels));
        try
        {
            for (var i = 0; i < length; i++)
            for (var j = 0; j < channels; j++)
            {
                var idx = i * channels + j;
                samples[idx] = data.Samples[j][i];
            }

            if (AL.IsBuffer(AudioBuffer))
            {
                // We can't update buffer data while it's being used by sources in some OpenAL implementations.
                // But usually AL.BufferData on an existing buffer is fine if we are careful.
                AL.BufferData(AudioBuffer, format, samples, -1, sampleRate);
            }
            else
            {
                AudioBuffer = AL.GenBuffer();
                AL.BufferData(AudioBuffer, format, samples, -1, sampleRate);
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(samples);
        }

        _context.CheckErrors();
        return true;
    }

    public override void Play(Action? callbackWhenFinished = null, bool autoRemove = true)
    {
        var audio_context = _context;
        var source = AL.GenSource();

        if (!AL.IsSource(source))
        {
            _logger.Error("Audio source ID '{Source}' isn't a valid source", source);
            return;
        }

        lock (_audioSources)
        {
            _audioSources.Add(source);
        }

        var size = AL.GetBufferi(AudioBuffer, BufferGetPNameI.Size);

        var bits = AL.GetBufferi(AudioBuffer, BufferGetPNameI.Bits);
        var channels = AL.GetBufferi(AudioBuffer, BufferGetPNameI.Channels);
        var frequency = AL.GetBufferi(AudioBuffer, BufferGetPNameI.Frequency);

        var size_per_channel = (float)size / channels;
        var samples = size_per_channel / (bits / 8f);

        var length = (int)(1000f * (samples / frequency));

        AL.Sourcei(source, SourcePNameI.Buffer, AudioBuffer);

        AL.Sourcef(source, SourcePNameF.Gain, Volume);
        AL.Source3f(source, SourcePName3F.Position, _pan, 0, 0);

        AL.SourcePlay(source);
        audio_context.CheckErrors();
        IsRunning = true;

        Task.Run(async () =>
        {
            if (!autoRemove) return;
            await Task.Delay(length);

            AL.DeleteSource(source);
            audio_context.CheckErrors();

            lock (_audioSources)
            {
                _audioSources.Remove(source);
            }

            callbackWhenFinished?.Invoke();
        });
    }

    public override void Stop()
    {
        lock (_audioSources)
        {
            foreach (var audio_source in _audioSources)
            {
                if (!AL.IsSource(audio_source)) return;
                AL.SourceStop(audio_source);
            }
        }
    }

    public override long GetTime_Milliseconds()
    {
        lock (_audioSources)
        {
            if (_audioSources.Count < 1) return -1;
            var source = _audioSources.FirstOrDefault();

            AL.GetSourcef(source, SourceGetPNameF.SecOffset, out var offset);
            return (long)(offset * 1000f);
        }
    }

    public override void SeekTime_Milliseconds(long milliseconds)
    {
        lock (_audioSources)
        {
            foreach (var source in _audioSources) AL.Sourcef(source, SourcePNameF.SecOffset, milliseconds / 1000f);
        }
    }

    public override void SetVolume(float volume)
    {
        Volume = volume;
        lock (_audioSources)
        {
            foreach (var source in _audioSources) AL.Sourcef(source, SourcePNameF.Gain, Volume);
        }
    }

    public override void Delete()
    {
        lock (_audioSources)
        {
            foreach (var audio_source in _audioSources)
            {
                if (!AL.IsSource(audio_source)) return;
                AL.SourceStop(audio_source);
            }
        }

        if (!AL.IsBuffer(AudioBuffer)) return;
        AL.DeleteBuffer(AudioBuffer);
        AudioBuffer = -1;
    }

    public override void SetPause(bool state)
    {
        lock (_audioSources)
        {
            if (_audioSources.Count < 1) return;
            foreach (var source in _audioSources)
            {
                AL.GetSourcei(source, SourceGetPNameI.SourceState, out var playing);

                var playing_state = (SourceState)playing;

                switch (playing_state)
                {
                    case SourceState.Initial when !state:
                    case SourceState.Paused when !state:
                        AL.SourcePlay(source);
                        IsRunning = true;
                        break;
                    case SourceState.Playing when state:
                        AL.SourcePause(source);
                        IsRunning = false;
                        break;
                }
            }
        }
    }

    public override void SetPan(float pan)
    {
        _pan = pan;
    }
}