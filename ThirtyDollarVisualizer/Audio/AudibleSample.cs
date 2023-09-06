using OpenTK.Audio.OpenAL;
using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio;

public class AudibleSample
{
    private readonly float[] intertweened_audio;
    private int _audio_source;
    private int _audio_buffer;
    private ALFormat _format;
    public float _volume => AudioContext.GlobalVolume * _relative_volume;
    public float _relative_volume = .5f;

    public static bool CheckErrors()
    {
        var has_error = false;
        ALError error;
        while ((error = AL.GetError()) != ALError.NoError)
        {
            has_error = true;
            Console.WriteLine($"[OpenAL Error]: (0x{(int)error:x8}) \'{error}\'");
        }

        return has_error;
    }
    
    public AudibleSample(AudioData<float> data)
    {
        var length = data.Samples[0].LongLength;
        var channels = data.ChannelCount;

        _format = channels switch
        {
            1 => ALFormat.MonoFloat32Ext,
            2 => ALFormat.StereoFloat32Ext,
            _ => throw new ArgumentOutOfRangeException(nameof(data), "The given channels count is invalid.")
        };

        var samples = new float[(int) length * (int) channels];
        for (var i = 0; i < length; i++)
        {
            for (var j = 0; j < channels; j++)
            {
                samples[i * channels + j] = data.Samples[i % channels][i];
            }
        }

        intertweened_audio = samples;

        CheckErrors();
        
        Cache();

        CheckErrors();
    }

    public void Cache()
    {
        _audio_source = AL.GenSource();
        _audio_buffer = AL.GenBuffer();
        
        AL.BufferData(_audio_buffer, _format, intertweened_audio, AudioContext.SampleRate);
        AL.Source(_audio_source, ALSourcei.Buffer, _audio_buffer);
    }

    public void SetVolume(float volume = 0.5f)
    {
        _relative_volume = volume;
    }

    public void PlaySample()
    {
        CheckErrors();
        AL.Source(_audio_source, ALSourcef.Gain, _volume);
        
        AL.SourceRewind(_audio_source);
        AL.SourcePlay(_audio_source);
    }

    public void Destroy()
    {
        AL.DeleteSource(_audio_source);
        AL.DeleteBuffer(_audio_buffer);
    }
}