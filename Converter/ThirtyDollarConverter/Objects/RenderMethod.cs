namespace ThirtyDollarConverter.Objects;

/// <summary>How <see cref="PcmEncoder" /> mixes a render.</summary>
public enum RenderMethod
{
    /// <summary>
    ///     Every sound as a voice in one start-sorted list, mixed straight into the output in
    ///     parallel slices. The default.
    /// </summary>
    Voice,

    /// <summary>
    ///     The old per-track renderer: a full-length buffer per separated sound, summed at the end.
    ///     Its output depends on <see cref="EncoderSettings.MultithreadingSlices" />.
    /// </summary>
    LegacyTracks
}
