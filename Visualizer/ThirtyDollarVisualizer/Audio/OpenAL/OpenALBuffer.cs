using OpenTK.Audio.OpenAL;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarVisualizer.Helpers.Logging;

namespace ThirtyDollarVisualizer.Audio.OpenAL;

public class OpenALBuffer : AudibleBuffer
{
    private readonly List<int> _audioSources = [];
    private readonly AudioContext _context;
    private float _pan;
    public float RelativeVolume = .5f;

    public OpenALBuffer(AudioContext context, AudioData<float> sampleData, int sampleRate)
    {
        var length = sampleData.Samples[0].LongLength;
        var channels = (int)sampleData.ChannelCount;
        _context = context;

        var format = channels switch
        {
            1 => Format.FormatMonoFloat32,
            2 => Format.FormatStereoFloat32,
            _ => throw new ArgumentOutOfRangeException(nameof(sampleData), "The given channels count is invalid.")
        };

        var samples = new float[(int)length * channels];
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

    public float Volume => RelativeVolume * _context.GlobalVolume;
    public int AudioBuffer { get; set; }

    public override void Play(Action? callbackWhenFinished = null, bool autoRemove = true)
    {
        var audio_context = _context;
        var source = AL.GenSource();

        if (!AL.IsSource(source))
        {
            DefaultLogger.Log("OpenAL Error", $"Audio source ID \'{source}\' isn't a valid source.");
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

    public override void SetVolume(float volume, bool absolute = false)
    {
        RelativeVolume = absolute ? volume * (1 / _context.GlobalVolume) : volume;
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
                        break;
                    case SourceState.Playing when state:
                        AL.SourcePause(source);
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