using Sundex.Core;
using ThirtyDollarConverter.Encoder.PCM;

namespace Shared.Audio.Null;

/// <summary>
///     A buffer that plays nothing but still keeps time. Silent is not timeless: every
///     scene reads its playhead off whatever buffer the sequence player is holding - see
///     <see cref="TimingStopwatchWrapper" /> - so a buffer with no clock of its own leaves
///     the editor transport and the visualizer playhead reading whatever it makes up.
///     This one used to say <c>long.MaxValue</c> and <c>IsRunning</c> from the moment it
///     was built, which is why <c>--no-audio</c> showed a 153722867280912:55 playhead and
///     a Pause button over a project that had never been started.
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
    ///     Starts playing from the beginning, which for a silent buffer means starting the
    ///     clock from the beginning. <see cref="SequencePlayer.Start" /> calls this rather
    ///     than <see cref="AudibleBuffer.Start" />, so a Play that only fired the callback
    ///     left the visualizer holding a loaded sequence that never advanced under
    ///     <c>--no-audio</c>. OpenAL plays a fresh source here, which starts at offset 0 -
    ///     hence the seek.
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
