namespace ThirtyDollarVisualizer.Objects;

public readonly struct PlayfieldSettings(
    float renderable_size,
    string download_location,
    float render_scale = 1f,
    int sounds_on_a_single_line = 16,
    int sound_size = 64,
    int sound_margin = 6)
{
    /// <summary>
    ///     Where the downloaded TDW assets are located.
    /// </summary>
    public readonly string DownloadLocation = download_location;

    /// <summary>
    ///     What's the current render scale of the window.
    /// </summary>
    public readonly float RenderScale = render_scale;

    /// <summary>
    ///     How big in pixels the value font will be.
    /// </summary>
    public readonly float ValueFontSize = renderable_size / 3.625f;

    /// <summary>
    ///     How big in pixels the volume font will be.
    /// </summary>
    public readonly float VolumeFontSize = renderable_size * 0.18f; // magic number that looks "just right"

    /// <summary>
    ///     How many sounds are contained on a single line.
    /// </summary>
    public readonly int SoundsOnASingleLine = sounds_on_a_single_line;

    /// <summary>
    ///     How big a sound is in pixels. Scaled by the render scale.
    /// </summary>
    public readonly int SoundSize = sound_size;

    /// <summary>
    ///     How big the gap between each side of a sound is in pixels. Scaled by the render scale.
    /// </summary>
    public readonly int SoundMargin = sound_margin;
}