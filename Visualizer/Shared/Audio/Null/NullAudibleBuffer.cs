using Sundex.Core;
using ThirtyDollarConverter.Encoder.PCM;

namespace Shared.Audio.Null;

/// <summary>
///     A buffer that plays nothing but still keeps time. Every scene reads its playhead off
///     whatever buffer the sequence player is holding - see
///     <see cref="TimingStopwatchWrapper" /> - so this one runs a real clock, and that clock
///     is what the editor transport and the visualizer playhead read under <c>--no-audio</c>.
/// </summary>
public class NullAudibleBuffer : AudibleBuffer
{
    public static readonly AudibleBuffer EmptyBuffer = new NullAudibleBuffer();

    private readonly SeekableStopwatch _clock = new();

    /// <summary>
    ///     Held by the clock rather than a field of its own, so pausing through any of the
    ///     base class's routes stops time as well as sound.
    /// </summary>
    public override bool IsRunning
    {
        get => _clock.IsRunning;
        protected set
        {
            if (value) _clock.Start();
            else _clock.Stop();
        }
    }

    // Methods that don't need an implementation.
    public override bool UploadNewData(AudioData<float> data, int sampleRate)
    {
        return true;
    }

    /// <summary>
    ///     Starts playing from the beginning, which for a silent buffer means restarting the
    ///     clock at zero. <see cref="SequencePlayer.Start" /> calls this rather than
    ///     <see cref="AudibleBuffer.Start" />, so the clock has to advance from here or a
    ///     loaded sequence never moves. The seek matches OpenAL, which plays a fresh source
    ///     from offset 0.
    /// </summary>
    public override void Play(Action? callbackWhenFinished = null, bool autoRemove = true)
    {
        _clock.Seek(0);
        _clock.Start();

        // ponytail: fires straight away rather than after the buffer's length - nothing
        // passes a callback here. Time it off the clock if something starts to.
        callbackWhenFinished?.Invoke();
    }

    public override long GetTime_Milliseconds()
    {
        return _clock.ElapsedMilliseconds;
    }

    public override void Stop()
    {
        IsRunning = false;
    }

    public override void SeekTime_Milliseconds(long milliseconds)
    {
        _clock.Seek(milliseconds);
    }

    public override void SetPause(bool state)
    {
        IsRunning = !state;
    }

    public override void SetVolume(float volume)
    {
    }

    public override void Delete()
    {
    }

    public override void SetPan(float pan)
    {
    }
}
