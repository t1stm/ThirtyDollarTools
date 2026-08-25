namespace EditorScene.Scenes.Views;

/// <summary>
///     How big the faithful editor draws a sound, and how many go on a line. These are the
///     website's own numbers, which are also
///     <c>VisualizerSettings.EventSize/EventMargin/LineAmount</c>'s defaults - copied rather
///     than read from there because the editor is not handed the visualizer's settings, and
///     the faithful views want the site's layout whatever the visualizer is tuned to.
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
///     The one box size the whole faithful editor draws at. The sequence decides it - its
///     sixteen-across rule is the only real constraint, and a panel narrower than sixteen
///     full-size boxes forces it below <see cref="FaithfulSizing.SoundSize" /> - and the
///     palettes follow, so a legend is never drawn at a different scale from the sequence it
///     inserts into. Shared by reference; read on the next layout, which runs every frame.
/// </summary>
public sealed class FaithfulScale
{
    /// <summary>
    ///     Raised when the size actually moved. A follower has to be told: its box size is
    ///     not part of its rectangle, so nothing else would ever make it re-measure.
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
