using System;

namespace ThirtyDollarConverter.Voices;

/// <summary>
///     What a block-at-a-time player reads a <see cref="VoiceList" /> through. The CPU's
///     <see cref="VoiceCursor" /> mixes each block when it is asked for; another implementation
///     may mix ahead somewhere else (a GPU) and hand blocks back once they are done, so a block
///     can be "not ready yet".
///     <para>
///         Every method belongs to one feeding thread. Implementations allocate nothing in steady
///         state, as the player's feeder thread doesn't.
///     </para>
/// </summary>
public interface IVoiceCursor : IDisposable
{
    /// <summary>The list being read; null renders silence.</summary>
    VoiceList? List { get; }

    /// <summary>Output sample the next block starts at.</summary>
    int Position { get; }

    /// <summary>
    ///     Switches to <paramref name="list" /> at <paramref name="position" /> and returns the list
    ///     it was reading, which the caller now disposes - the cursor holds no reference of its own
    ///     that outlives it.
    /// </summary>
    VoiceList? Attach(VoiceList? list, int position);

    /// <summary>Moves to <paramref name="position" />, dropping anything mixed ahead.</summary>
    void Seek(int position);

    /// <summary>
    ///     Writes the next <c>left.Length</c> samples into <paramref name="left" /> and
    ///     <paramref name="right" /> (overwriting them) and advances, or returns false and changes
    ///     nothing when they aren't mixed yet. <paramref name="force" /> says the caller is running
    ///     dry: an implementation that mixes elsewhere should mix the block itself if waiting would
    ///     cost more. Both spans must be the same length; a mono list leaves <paramref name="right" />
    ///     silent.
    /// </summary>
    bool TryRender(Span<float> left, Span<float> right, bool force);
}
