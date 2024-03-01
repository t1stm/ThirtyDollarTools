using OpenTK.Audio.OpenAL;
using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio;

public class OpenALBuffer : AudibleBuffer
{
    public float _volume => _relative_volume * _context.GlobalVolume;
    public float _relative_volume = .5f;
    private readonly AudioContext _context;
    private List<int> AudioSources = new();
    public int AudioBuffer { get; set; }
    private float _pan;

    public OpenALBuffer(AudioContext context, AudioData<float> sample_data, int sample_rate)
    {
        var length = sample_data.Samples[0].LongLength;
        var channels = (int) sample_data.ChannelCount;
        _context = context;

        var format = channels switch
        {
            1 => ALFormat.MonoFloat32Ext,
            2 => ALFormat.StereoFloat32Ext,
            _ => throw new ArgumentOutOfRangeException(nameof(sample_data), "The given channels count is invalid.")
        };

        var samples = new float[(int)length * channels];
        var samples_span = samples.AsSpan();
        for (var i = 0; i < length; i++)
        {
            for (var j = 0; j < channels; j++)
            {
                var idx = i * channels + j;
                samples_span[idx] = sample_data.Samples[i % channels][i];
            }
        }

        AudioBuffer = AL.GenBuffer();
        AL.BufferData(AudioBuffer, format, samples, sample_rate);
    }
    public override void Play(Action? callback_when_finished = null, bool auto_remove = true)
    {
        var audio_context = _context;
        var source = AL.GenSource();

        if (!AL.IsSource(source))
        {
            Console.WriteLine($"({DateTime.Now:G}): [OpenAL Error]: Audio source ID isn't a valid source.");
            return;
        }
        lock (AudioSources)
            AudioSources.Add(source);

        var size = AL.GetBuffer(AudioBuffer, ALGetBufferi.Size);
        
        var bits = AL.GetBuffer(AudioBuffer, ALGetBufferi.Bits);
        var channels = AL.GetBuffer(AudioBuffer, ALGetBufferi.Channels);
        var frequency = AL.GetBuffer(AudioBuffer, ALGetBufferi.Frequency);

        var size_per_channel = (float) size / channels;
        var samples = size_per_channel / (bits / 8f);
        
        var length = (int) (1000f * (samples / frequency));
        
        AL.Source(source, ALSourcei.Buffer, AudioBuffer);
        AL.Source(source, ALSourcef.Gain, _volume);
        AL.Source(source, ALSource3f.Position, _pan,0,0);
        
        AL.SourcePlay(source);
        audio_context.CheckErrors();

        Task.Run(async () =>
        {
            if (!auto_remove) return;
            await Task.Delay(length);
            
            AL.DeleteSource(source);
            audio_context.CheckErrors();

            lock (AudioSources)
                AudioSources.Remove(source);
            callback_when_finished?.Invoke();
        });
    }

    public override void Stop()
    {
        lock (AudioSources)
            foreach (var audio_source in AudioSources)
            {
                if (!AL.IsSource(audio_source)) return;
                AL.SourceStop(audio_source);
            }
    }

    public override long GetTime_Milliseconds()
    {
        lock (AudioSources)
        {
            if (AudioSources.Count < 1) return -1;
            var source = AudioSources.FirstOrDefault();
        
            AL.GetSource(source, ALSourcef.SecOffset, out var offset);
            return (long)(offset * 1000f);
        }
    }

    public override void SeekTime_Milliseconds(long milliseconds)
    {
        lock (AudioSources)
        {
            foreach (var source in AudioSources)
            {
                AL.Source(source, ALSourcef.SecOffset, milliseconds / 1000f);
            }
        }
    }

    public override void SetVolume(float volume)
    {
        _relative_volume = volume;
    }

    public override void Delete()
    {
        lock (AudioSources)
            foreach (var audio_source in AudioSources)
            {
                if (!AL.IsSource(audio_source)) return;
                AL.SourceStop(audio_source);
            }

        if (!AL.IsBuffer(AudioBuffer)) return;
        AL.DeleteBuffer(AudioBuffer);
        AudioBuffer = -1;
    }

    public override void SetPause(bool state)
    {
        lock (AudioSources)
        {
            if (AudioSources.Count < 1) return;
            foreach (var source in AudioSources)
            {
                AL.GetSource(source, ALGetSourcei.SourceState, out var playing);

                var playing_state = (ALSourceState) playing;

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