using OpenTK.Audio.OpenAL;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarVisualizer.Helpers.Logging;

namespace ThirtyDollarVisualizer.Audio;

public class BackingAudio(AudioContext context, AudioData<float> data, int sampleRate)
{
    private readonly AudibleBuffer _buffer = context.GetBufferObject(data, sampleRate);

    public long GetCurrentTime()
    {
        return _buffer.GetTime_Milliseconds();
    }

    public void Play()
    {
        _buffer.SetVolume(1f, true);
        _buffer.Play();
    }

    public void SyncTime(TimeSpan playerTime)
    {
        var time = GetCurrentTime() / 1000f;

        var delta = Math.Abs(time - (float)playerTime.TotalSeconds);
        if (delta <= 0.050f) return; // 50 milliseconds 

        var position = (long)(playerTime.TotalSeconds * 1000);
        DefaultLogger.Log("Backing Audio", $"Out of sync. Syncing. Delta: {delta} / Time: {time} / Position: {position}");
        _buffer.SeekTime_Milliseconds(position);
    }

    public void UpdatePlayState(bool playing)
    {
        _buffer.SetPause(!playing);
    }
}