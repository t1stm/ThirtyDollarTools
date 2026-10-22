namespace ThirtyDollarConverter.Voices;

/// <summary>
///     One audible sound placement: which resampled sound it plays, where it starts and ends in
///     the output, and how loud each output channel takes each source channel. A struct, so a
///     <see cref="VoiceList" /> is one array rather than an object per voice.
/// </summary>
public struct Voice
{
    /// <summary>Output sample the voice starts on.</summary>
    public int Start;

    /// <summary>
    ///     Output sample the voice stops before: its natural end, clipped to the song, or the
    ///     end of its fade when it was cut.
    /// </summary>
    public int End;

    /// <summary>
    ///     Output sample a cut starts fading the voice at, or <see cref="int.MaxValue" /> when
    ///     nothing cuts it.
    /// </summary>
    public int CutAt;

    /// <summary>Sample of the sound that plays at <see cref="Start" /> (an offset event skips its head).</summary>
    public int SourceOffset;

    /// <summary>Index into <see cref="VoiceList" />'s sounds table.</summary>
    public int Sound;

    /// <summary>Gains, volume scale already applied: left from the left source channel, and so on.</summary>
    public float LeftFromLeft, RightFromRight, LeftFromRight, RightFromLeft;
}
