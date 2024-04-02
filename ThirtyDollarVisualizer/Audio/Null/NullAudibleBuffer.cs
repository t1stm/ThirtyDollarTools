using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio.Null;

public class NullAudibleBuffer : AudibleBuffer
{
    public static readonly AudibleBuffer EmptyBuffer = new NullAudibleBuffer(AudioData<float>.Empty(2), 48000);

    public NullAudibleBuffer(AudioData<float> data, int sample_rate)
    {
    }

    // Methods with some implementation.
    public override void Play(Action? callback_when_finished = null, bool auto_remove = true)
    {
        callback_when_finished?.Invoke();
    }

    public override long GetTime_Milliseconds()
    {
        return long.MaxValue;
    }

    // Methods that don't need an implementation.
    public override void Stop()
    {
    }

    public override void SeekTime_Milliseconds(long milliseconds)
    {
    }

    public override void SetVolume(float volume)
    {
    }

    public override void Delete()
    {
    }

    public override void SetPause(bool state)
    {
    }

    public override void SetPan(float pan)
    {
    }
}