namespace Shared.Audio;

/// <summary>
///     A channel fed a block at a time: <see cref="Put" /> queues interleaved float frames in the
///     audio library's own native memory, and its device thread plays them from there. Nothing on
///     the playing side runs managed code, so a garbage collection only stalls whoever puts, and
///     the queue plays on through it - see <see cref="VoicePlayback" />.
///     <para>
///         <see cref="Put" />, <see cref="QueuedFrames" /> and <see cref="Flush" /> belong to one
///         feeding thread; the rest may be called from any thread. None of them allocate.
///     </para>
/// </summary>
public abstract class AudioStream
{
    /// <summary>Frames put but not played yet, whatever the library holds them in.</summary>
    public abstract long QueuedFrames { get; }

    /// <summary>
    ///     Frames played since the last <see cref="Flush" />, plus the position that flush started
    ///     from: the playhead, in frames. Stops where the data runs out.
    /// </summary>
    public abstract long PlayedPosition { get; }

    /// <summary>Queues <paramref name="interleaved" /> - whole frames - after everything put so far.</summary>
    public abstract void Put(ReadOnlySpan<float> interleaved);

    /// <summary>
    ///     Drops every frame not played yet, and restarts <see cref="PlayedPosition" /> at
    ///     <paramref name="basePosition" />. Leaves play/pause as it was.
    /// </summary>
    public abstract void Flush(long basePosition);

    /// <summary>Plays - or, while the queue is empty, starts as soon as something is put.</summary>
    public abstract void Play();

    public abstract void SetPause(bool paused);

    /// <summary>Linear gain: 1 plays the data as it is, past 1 amplifies it.</summary>
    public abstract void SetVolume(float volume);

    public abstract void Delete();
}
