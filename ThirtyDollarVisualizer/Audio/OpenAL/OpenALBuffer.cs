using OpenTK.Audio.OpenAL;
using ThirtyDollarEncoder.PCM;

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
            1 => ALFormat.MonoFloat32Ext,
            2 => ALFormat.StereoFloat32Ext,
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
        AL.BufferData(AudioBuffer, format, samples, sampleRate);
    }

    public float Volume => RelativeVolume * _context.GlobalVolume;
    public int AudioBuffer { get; set; }

    public override void Play(Action? callbackWhenFinished = null, bool autoRemove = true)
    {
        var audio_context = _context;
        var source = AL.GenSource();

        if (!AL.IsSource(source))
        {
            Console.WriteLine($"({DateTime.Now:G}): [OpenAL Error]: Audio source ID isn't a valid source.");
            return;
        }

        lock (_audioSources)
        {
            _audioSources.Add(source);
        }

        var size = AL.GetBuffer(AudioBuffer, ALGetBufferi.Size);

        var bits = AL.GetBuffer(AudioBuffer, ALGetBufferi.Bits);
        var channels = AL.GetBuffer(AudioBuffer, ALGetBufferi.Channels);
        var frequency = AL.GetBuffer(AudioBuffer, ALGetBufferi.Frequency);

        var size_per_channel = (float)size / channels;
        var samples = size_per_channel / (bits / 8f);

        var length = (int)(1000f * (samples / frequency));

        AL.Source(source, ALSourcei.Buffer, AudioBuffer);

        AL.Source(source, ALSourcef.Gain, Volume);
        AL.Source(source, ALSource3f.Position, _pan, 0, 0);

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

            AL.GetSource(source, ALSourcef.SecOffset, out var offset);
            return (long)(offset * 1000f);
        }
    }

    public override void SeekTime_Milliseconds(long milliseconds)
    {
        lock (_audioSources)
        {
            foreach (var source in _audioSources) AL.Source(source, ALSourcef.SecOffset, milliseconds / 1000f);
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
                AL.GetSource(source, ALGetSourcei.SourceState, out var playing);

                var playing_state = (ALSourceState)playing;

                switch (playing_state)
                {
                    case ALSourceState.Initial when !state:
                    case ALSourceState.Paused when !state:
                        AL.SourcePlay(source);
                        break;
                    case ALSourceState.Playing when state:
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