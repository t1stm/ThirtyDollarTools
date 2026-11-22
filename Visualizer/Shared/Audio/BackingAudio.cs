using Sundex.Engine.Audio;
using ThirtyDollarConverter.Encoder.PCM;

namespace Shared.Audio;

/// <summary>The visualizer's backing track: one voice over the whole file, kept within 50 ms of the song.</summary>
public class BackingAudio(AudioDevice device, AudioData<float> data, int sampleRate)
{
    private readonly AudioBuffer _buffer = device.CreateBuffer(data, sampleRate);
    private AudioVoice? _voice;

    private AudioVoice Voice => _voice ??= _buffer.CreateVoice();

    public long GetCurrentTime()
    {
        return Voice.Position * 1000 / sampleRate;
    }

    public void Play()
    {
        Voice.Volume = 1f;
        Voice.Seek(0);
        Voice.Paused = false;
    }

    public void SyncTime(TimeSpan playerTime)
    {
        var time = GetCurrentTime() / 1000f;

        var delta = Math.Abs(time - (float)playerTime.TotalSeconds);
        if (delta <= 0.050f) return; // 50 milliseconds 

        Voice.Seek((long)(playerTime.TotalSeconds * sampleRate));
    }

    public void UpdatePlayState(bool playing)
    {
        Voice.Paused = !playing;
    }

    public void Stop()
    {
        _buffer.Dispose();
    }
}
