namespace ThirtyDollarVisualizer.Objects;

public readonly struct PlayfieldSettings(float renderable_size, string download_location, float render_scale = 1f)
{
    /// <summary>
    /// Where the downloaded TDW assets are located.
    /// </summary>
    public readonly string DownloadLocation = download_location;
    
    /// <summary>
    /// What's the current render scale of the window. 
    /// </summary>
    public readonly float RenderScale = render_scale;

    /// <summary>
    /// How big in pixels the value font will be.
    /// </summary>
    public readonly float ValueFontSize = renderable_size / 3.625f;
    
    /// <summary>
    /// How big in pixels the volume font will be.
    /// </summary>
    public readonly float VolumeFontSize = renderable_size * 0.22f; // magic number that looks "just right"
}