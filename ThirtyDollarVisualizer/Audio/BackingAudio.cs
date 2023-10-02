using OpenTK.Audio.OpenAL;
using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio;

public class BackingAudio : AudibleBuffer
{
    public BackingAudio(AudioData<float> data, int sample_rate) : base(data, sample_rate)
    {
    }

    public float GetCurrentTime()
    {
        if (AudioSources.Count < 1) return -1;
        var source = AudioSources.FirstOrDefault();
        
        AL.GetSource(source, ALSourcef.SecOffset, out var offset);
        return offset;
    }

    public void Play(AudioContext context)
    {
        PlaySample(context, null, false);
    }

    public void SyncTime(TimeSpan player_time)
    {
        if (AudioSources.Count < 1) return;
        var source = AudioSources.FirstOrDefault();

        AL.GetSource(source, ALSourcef.SecOffset, out var seconds);

        var delta = Math.Abs(seconds - (float)player_time.TotalSeconds);
        if (delta > 0.050f) // 50 milliseconds 
            AL.Source(source, ALSourcef.SecOffset, (float) player_time.TotalSeconds);
    }

    public void UpdatePlayState(bool playing)
    {
        if (AudioSources.Count < 1) return;
        var source = AudioSources.FirstOrDefault();
        AL.GetSource(source, ALGetSourcei.SourceState, out var state);

        var playing_state = (ALSourceState) state;

        switch (playing_state)
        {
            case ALSourceState.Initial when playing:
            case ALSourceState.Paused when playing:
                AL.SourcePlay(source);
                break;
            case ALSourceState.Playing when !playing:
                AL.SourcePause(source);
                break;
        }
    }
}