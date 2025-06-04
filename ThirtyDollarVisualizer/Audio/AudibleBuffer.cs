namespace ThirtyDollarVisualizer.Audio;

public abstract class AudibleBuffer
{
    public abstract void Play(Action? callbackWhenFinished = null, bool autoRemove = true);
    public abstract void Stop();
    public abstract long GetTime_Milliseconds();
    public abstract void SeekTime_Milliseconds(long milliseconds);
    public abstract void SetVolume(float volume, bool absolute = false);
    public abstract void Delete();
    public abstract void SetPause(bool state);
    public abstract void SetPan(float pan);
}