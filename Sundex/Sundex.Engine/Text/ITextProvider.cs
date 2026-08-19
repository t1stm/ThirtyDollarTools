using OpenTK.Mathematics;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Text.Fonts;

namespace Sundex.Engine.Text;

/// <summary>
///     Abstraction over <see cref="TextProvider" /> to allow mock implementations for testing.
/// </summary>
public interface ITextProvider
{
    IGlyphProvider GlyphProvider { get; }

    float TextureWidth { get; }
    float TextureHeight { get; }

    /// <summary>
    ///     Returns the UV rectangle and alignment data for a single character glyph.
    /// </summary>
    (Vector4, TextAlignmentData) GetTextCharacterRect(ReadOnlySpan<char> character);

    /// <summary>
    ///     Binds the text atlas to the OpenGL context and activates the shader program.
    /// </summary>
    void BindAndSetUniforms(Camera camera);

    /// <summary>
    ///     Generates the glyphs for every character in <paramref name="characters" /> ahead
    ///     of the frame that first draws them, so <see cref="GetTextCharacterRect" /> is left
    ///     with only the upload to do. Safe to call from any thread, and cheap to call again
    ///     for characters already generated or already in the atlas.
    /// </summary>
    void Warm(string characters)
    {
    }
}