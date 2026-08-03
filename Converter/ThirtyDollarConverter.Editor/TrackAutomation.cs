namespace ThirtyDollarConverter.Editor;

/// <summary>
///     One keyframe manager applied to every matching note in a track, rather than a
///     single note. The manager is stateless during expansion (see
///     <see cref="AudioKeyframeManager" />), so the same instance expands independently
///     for every note it applies to.
/// </summary>
public class TrackAutomation
{
    public required AudioKeyframeManager Keyframes { get; set; }

    /// <summary>Null = applies to every sound in the track.</summary>
    public List<string>? Sounds { get; set; }
}