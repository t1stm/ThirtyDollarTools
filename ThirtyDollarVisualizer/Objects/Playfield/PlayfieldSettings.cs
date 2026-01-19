using OpenTK.Mathematics;
using ThirtyDollarConverter;
using ThirtyDollarVisualizer.Objects.Playfield.Atlas;
using ThirtyDollarVisualizer.Scenes.Application;

namespace ThirtyDollarVisualizer.Objects.Playfield;

public class PlayfieldSettings
{
    private static readonly Vector4 DefaultBackgroundColor = new(0.22f, 0.22f, 0.22f, 1);

    /// <summary>
    ///     A container for managing audio samples, their associated metadata,
    ///     and utility methods for downloading and organizing sample-related resources.
    /// </summary>
    public required SampleHolder SampleHolder { get; set; }

    /// <summary>
    ///     Encapsulates configuration settings related to the sizing and layout of the playfield,
    ///     including parameters that determine font sizes, margins, and sound arrangements.
    /// </summary>
    public required PlayfieldSizing PlayfieldSizing { get; set; }

    /// <summary>
    ///     What the current render scale of the window is.
    /// </summary>
    public required float RenderScale { get; set; }

    /// <summary>
    ///     A container for global texture atlases.
    /// </summary>
    public required AtlasStore AtlasStore { get; set; }

    /// <summary>
    ///     A collection of application-wide font providers utilized for rendering text with various font styles.
    /// </summary>
    public required ApplicationFonts Fonts { get; set; }

    public float ScrollSpeed { get; set; } = 6f;

    public Vector4 BackgroundColor { get; set; } = DefaultBackgroundColor;
}