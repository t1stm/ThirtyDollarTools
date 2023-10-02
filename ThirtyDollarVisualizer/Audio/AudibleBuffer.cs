using OpenTK.Audio.OpenAL;
using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio;

public class AudibleBuffer : IDisposable
{
    protected int AudioBuffer;
    protected readonly List<int> AudioSources = new();
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

        AudioBuffer = AL.GenBuffer();
        AL.BufferData(AudioBuffer, format, samples, sample_rate);
    }

    public void SetVolume(float volume = 0.5f)
    {
        _relative_volume = volume;
    }

    public void PlaySample(AudioContext context, Action? callback_when_finished = null, bool auto_remove = true)
    {
        var audio_context = context.context;
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
        
        AL.SourcePlay(source);
        context.CheckErrors();

        Task.Run(async () =>
        {
            if (!auto_remove) return;
            
            await Task.Delay(length);
            if (context.context != audio_context) return;
            
            AL.DeleteSource(source);
            context.CheckErrors();

            lock (AudioSources)
                AudioSources.Remove(source);
            callback_when_finished?.Invoke();
        });
    }

    public void Stop()
    {
        lock (AudioSources)
            foreach (var audio_source in AudioSources)
            {
                if (!AL.IsSource(audio_source)) return;
                AL.SourceStop(audio_source);
            }
    }

    public void Destroy()
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

    public void Dispose()
    {
        Destroy();
        GC.SuppressFinalize(this);
    }
}