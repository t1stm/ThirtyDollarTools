using OpenTK.Audio.OpenAL;
using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio;

public class AudibleSample : IDisposable
{
    private readonly float[] intertweened_audio;
    private int _audio_buffer;
    private int _audio_source;
    private readonly ALFormat _format;
    public float _volume => AudioContext.GlobalVolume * _relative_volume;
    public float _relative_volume = .5f;

    private bool _destroy = false;
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
        _audio_buffer = AL.GenBuffer();
        AL.BufferData(_audio_buffer, _format, intertweened_audio, AudioContext.SampleRate);
    }

    public void SetVolume(float volume = 0.5f)
    {
        _relative_volume = volume;
    }

    public async Task PlaySample()
    {
        CheckErrors();
        _audio_source = AL.GenSource();
        var source = _audio_source;
        
        AL.Source(source, ALSourcei.Buffer, _audio_buffer);
        AL.Source(source, ALSourcef.Gain, _volume);
        AL.SourcePlay(source);
        
        while (!_destroy)
        {
            AL.GetSource(source, ALGetSourcei.SourceState, out var state);

            if ((ALSourceState) state != ALSourceState.Stopped)
            {
                await Task.Delay(166);
                continue;
            }
            
            AL.DeleteSource(source);
            break;
        }
    }

    public void Stop()
    {
        AL.SourceStop(_audio_source);
    }

    public void Destroy()
    {
        if (_audio_buffer == -1) return;

        _destroy = true;
        AL.DeleteBuffer(_audio_buffer);
        _audio_buffer = -1;
    }

    public void Dispose()
    {
        Destroy();
        GC.SuppressFinalize(this);
    }
}