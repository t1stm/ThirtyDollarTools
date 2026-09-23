using Shared.Audio.Features;
using ThirtyDollarConverter.Encoder.PCM;

namespace Shared.Audio;

public abstract class AudibleBuffer : IBufferStopwatch
{
    public TimeSpan Elapsed => TimeSpan.FromMilliseconds(ElapsedMilliseconds);
    public long ElapsedMilliseconds => GetTime_Milliseconds();
    public abstract bool IsRunning { get; protected set; }

    public void Start()
    {
        Play();
        IsRunning = true;
    }

    public abstract void Stop();

    public void Restart()
    {
        SeekTime_Milliseconds(0);
        SetPause(false);
        IsRunning = true;
    }

    public void Reset()
    {
        SeekTime_Milliseconds(0);
        SetPause(true);
        IsRunning = false;
    }

    public void Seek(long milliseconds)
    {
        SeekTime_Milliseconds(milliseconds);
    }

    public abstract bool UploadNewData(AudioData<float> data, int sampleRate);
    public abstract void Play(Action? callbackWhenFinished = null, bool autoRemove = true);
    public abstract long GetTime_Milliseconds();
    public abstract void SeekTime_Milliseconds(long milliseconds);
    public abstract void SetVolume(float volume);
    public abstract void Delete();
    public abstract void SetPause(bool state);
    public abstract void SetPan(float pan);

    /// <summary>
    ///     A voice of its own on this buffer's data - see <see cref="AudioVoice" />. The
    ///     buffer's own playback calls can reach its voices too (BASS pauses and seeks every
    ///     channel of a sample at once), so a buffer that hands out voices is used for nothing
    ///     else but <see cref="Delete" />.
    /// </summary>
    public abstract AudioVoice NewVoice();
}