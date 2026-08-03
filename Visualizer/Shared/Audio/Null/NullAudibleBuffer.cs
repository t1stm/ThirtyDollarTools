using ThirtyDollarConverter.Encoder.PCM;

namespace Shared.Audio.Null;

public class NullAudibleBuffer : AudibleBuffer
{
    public static readonly AudibleBuffer EmptyBuffer = new NullAudibleBuffer();

    // Methods that don't need an implementation.
    public override bool IsRunning { get; protected set; } = true;

    // Methods with some implementation.
    public override bool UploadNewData(AudioData<float> data, int sampleRate)
    {
        return true;
    }

    public override void Play(Action? callbackWhenFinished = null, bool autoRemove = true)
    {
        callbackWhenFinished?.Invoke();
    }

    public override long GetTime_Milliseconds()
    {
        return long.MaxValue;
    }

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