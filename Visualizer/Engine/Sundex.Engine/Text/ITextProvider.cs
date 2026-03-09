using OpenTK.Mathematics;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Text.Fonts;

namespace Sundex.Engine.Text;

/// <summary>
///     Abstraction over <see cref="TextProvider" /> to allow mock implementations for testing.
/// </summary>
public interface ITextProvider
{
    /// <summary>
    ///     Returns the UV rectangle and alignment data for a single character glyph.
    /// </summary>
    (Vector4, TextAlignmentData) GetTextCharacterRect(ReadOnlySpan<char> character);

    /// <summary>
    ///     Binds the text atlas to the OpenGL context and activates the shader program.
    /// </summary>
    void BindAndSetUniforms(Camera camera);
}


