namespace Shared.Audio;

/// <summary>
///     A playback head of its own on an <see cref="AudibleBuffer" />'s data, from
///     <see cref="AudibleBuffer.NewVoice" />: its own position, pause state and volume, over
///     samples it shares rather than copies. Starts paused at the beginning. The editor's wave
///     references are what it is for - every clip of a file runs a voice on the file's one
///     buffer, where each used to upload a whole copy of the file.
///     Deleting a voice leaves the buffer as it is; deleting the buffer ends every voice on it,
///     so the voices go first.
/// </summary>
public abstract class AudioVoice
{
    public abstract long GetTime_Milliseconds();
    public abstract void SeekTime_Milliseconds(long milliseconds);
    public abstract void SetPause(bool paused);

    /// <summary>Linear gain: 1 plays the data as it is, past 1 amplifies it.</summary>
    public abstract void SetVolume(float volume);

    public abstract void Delete();
}
