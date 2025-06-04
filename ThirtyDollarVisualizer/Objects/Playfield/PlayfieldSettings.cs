namespace ThirtyDollarVisualizer.Objects.Playfield;

public readonly struct PlayfieldSettings(
    float renderableSize,
    string downloadLocation,
    float renderScale = 1f,
    int soundsOnASingleLine = 16,
    int soundSize = 64,
    int soundMargin = 6)
{
    /// <summary>
    ///     Where the downloaded TDW assets are located.
    /// </summary>
    public readonly string DownloadLocation = downloadLocation;

    /// <summary>
    ///     What the current render scale of the window is.
    /// </summary>
    public readonly float RenderScale = renderScale;

    /// <summary>
    ///     How big in pixels the value font will be.
    /// </summary>
    public readonly float ValueFontSize = renderableSize / 3.625f;

    /// <summary>
    ///     How big in pixels the volume font will be.
    /// </summary>
    public readonly float VolumeFontSize = renderableSize * 0.22f; // magic number that looks "just right"

    /// <summary>
    ///     How many sounds are contained on a single line.
    /// </summary>
    public readonly int SoundsOnASingleLine = soundsOnASingleLine;

    /// <summary>
    ///     How big a sound is in pixels. Scaled by the render scale.
    /// </summary>
    public readonly int SoundSize = soundSize;

    /// <summary>
    ///     How big the gap between each side of a sound is in pixels. Scaled by the render scale.
    /// </summary>
    public readonly int SoundMargin = soundMargin;
}