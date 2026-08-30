namespace EditorScene.Scenes.Views;

/// <summary>
///     How big the faithful editor draws a sound, and how many go on a line: the website's
///     own numbers, also <c>VisualizerSettings.EventSize/EventMargin/LineAmount</c>'s
///     defaults. Held here rather than read from the visualizer's settings, so the faithful
///     views keep the site's layout whatever the visualizer is tuned to.
/// </summary>
internal static class FaithfulSizing
{
    public const float SoundSize = 64f;
    public const float Margin = 12f;

    /// <summary>
    ///     Sixteen across, as on the site. The sequence keeps the count and shrinks the boxes
    ///     when the panel is too narrow for sixteen full-size ones - see
    ///     <see cref="EventCanvas.FitPerLine" />.
    /// </summary>
    public const int PerLine = 16;
}

/// <summary>
///     The one box size the whole faithful editor draws at. The sequence sets it - dropping
///     below <see cref="FaithfulSizing.SoundSize" /> when the panel is too narrow for sixteen
///     across - and the palettes follow, so a legend is never drawn at a different scale from
///     the sequence it inserts into. Shared by reference; read on the next layout.
/// </summary>
public sealed class FaithfulScale
{
    /// <summary>
    ///     Raised when the size actually moved. Followers must subscribe: box size is not part
    ///     of an element's rectangle, so nothing else makes them re-measure.
    /// </summary>
    public event Action? Changed;

    public float BoxSize
    {
        get;
        set
        {
            if (Math.Abs(field - value) < 0.5f) return;
            field = value;
            Changed?.Invoke();
        }
    } = FaithfulSizing.SoundSize;
}
