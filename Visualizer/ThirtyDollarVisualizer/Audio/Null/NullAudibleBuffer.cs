using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio.Null;

public class NullAudibleBuffer : AudibleBuffer
{
    public static readonly AudibleBuffer EmptyBuffer = new NullAudibleBuffer(AudioData<float>.Empty(2), 48000);

    public NullAudibleBuffer(AudioData<float> data, int sampleRate)
    {
    }

    // Methods with some implementation.
    public override void Play(Action? callbackWhenFinished = null, bool autoRemove = true)
    {
        callbackWhenFinished?.Invoke();
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

    public override void SetVolume(float volume, bool absolute = false)
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