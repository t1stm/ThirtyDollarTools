using OpenTK.Audio.OpenAL;
using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio;

public class AudibleBuffer : IDisposable
{
    private int _audio_buffer;
    private readonly List<int> _audio_sources = new();
    public float _volume => _relative_volume;
    public float _relative_volume = .5f;

    public AudibleBuffer(AudioData<float> data, int sample_rate)
    {
        var length = data.Samples[0].LongLength;
        var channels = data.ChannelCount;

        var format = channels switch
        {
            1 => ALFormat.MonoFloat32Ext,
            2 => ALFormat.StereoFloat32Ext,
            _ => throw new ArgumentOutOfRangeException(nameof(data), "The given channels count is invalid.")
        };

        var samples = new float[(int)length * (int)channels];
        for (var i = 0; i < length; i++)
        {
            for (var j = 0; j < channels; j++)
            {
                samples[i * channels + j] = data.Samples[i % channels][i];
            }
        }

        _audio_buffer = AL.GenBuffer();
        AL.BufferData(_audio_buffer, format, samples, sample_rate);
    }

    public void SetVolume(float volume = 0.5f)
    {
        _relative_volume = volume;
    }

    public void PlaySample(AudioContext context, Action? callback_when_finished = null)
    {
        var audio_context = context.context;
        var source = AL.GenSource();

        if (!AL.IsSource(source))
        {
            Console.WriteLine($"({DateTime.Now:G}): [OpenAL Error]: Audio source ID isn't a valid source.");
            return;
        }
        lock (_audio_sources)
            _audio_sources.Add(source);

        var size = AL.GetBuffer(_audio_buffer, ALGetBufferi.Size);
        
        var bits = AL.GetBuffer(_audio_buffer, ALGetBufferi.Bits);
        var channels = AL.GetBuffer(_audio_buffer, ALGetBufferi.Channels);
        var frequency = AL.GetBuffer(_audio_buffer, ALGetBufferi.Frequency);

        var size_per_channel = (float) size / channels;
        var samples = size_per_channel / (bits / 8f);
        
        var length = (int) (1000f * (samples / frequency));
        
        AL.Source(source, ALSourcei.Buffer, _audio_buffer);
        AL.Source(source, ALSourcef.Gain, _volume);
        
        AL.SourcePlay(source);
        context.CheckErrors();

        Task.Run(async () =>
        {
            await Task.Delay(length);
            if (context.context != audio_context) return;
            
            AL.DeleteSource(source);
            context.CheckErrors();

            lock (_audio_sources)
                _audio_sources.Remove(source);
            callback_when_finished?.Invoke();
        });
    }

    public void Stop()
    {
        lock (_audio_sources)
            foreach (var audio_source in _audio_sources)
            {
                if (!AL.IsSource(audio_source)) return;
                AL.SourceStop(audio_source);
            }
    }

    public void Destroy()
    {
        lock (_audio_sources)
            foreach (var audio_source in _audio_sources)
            {
                if (!AL.IsSource(audio_source)) return;
                AL.SourceStop(audio_source);
            }

        if (!AL.IsBuffer(_audio_buffer)) return;
        AL.DeleteBuffer(_audio_buffer);
        _audio_buffer = -1;
    }

    public void Dispose()
    {
        Destroy();
        GC.SuppressFinalize(this);
    }
}