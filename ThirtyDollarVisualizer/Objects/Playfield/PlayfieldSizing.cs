namespace ThirtyDollarVisualizer.Objects.Playfield;

public class PlayfieldSizing(int renderableSize)
{
    /// <summary>
    ///     How big a sound is in pixels. Scaled by the render scale.
    /// </summary>
    public int SoundSize { get; set; } = renderableSize;

    /// <summary>
    ///     How big in pixels the value font will be.
    /// </summary>
    public float ValueFontSize { get; set; } = renderableSize / 3.625f;

    /// <summary>
    ///     How big in pixels the volume font will be.
    /// </summary>
    public float VolumeFontSize { get; set; } = renderableSize * 0.18f; // magic number that looks "just right"

    /// <summary>
    ///     How big in pixels the volume font will be.
    /// </summary>
    public float PanFontSize { get; set; } = renderableSize * 0.18f; // magic number that looks "just right"

    /// <summary>
    ///     How many sounds are contained on a single line.
    /// </summary>
    public int SoundsOnASingleLine { get; set; } = 16;

    /// <summary>
    ///     How big the gap between each side of a sound is in pixels. Scaled by the render scale.
    /// </summary>
    public int SoundMargin { get; set; } = 6;
}