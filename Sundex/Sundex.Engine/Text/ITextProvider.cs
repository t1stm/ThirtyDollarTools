using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Text.Fonts;
using Sundex.Slug;

namespace Sundex.Engine.Text;

/// <summary>
///     Abstraction over <see cref="TextProvider" /> to allow mock implementations for testing.
/// </summary>
public interface ITextProvider
{
    IGlyphProvider GlyphProvider { get; }

    /// <summary>
    ///     The Slug data and layout for a single character glyph. Safe to call from any thread.
    /// </summary>
    SlugGlyph GetGlyph(ReadOnlySpan<char> character) => GlyphProvider.GetSlugGlyph(character);

    /// <summary>
    ///     Binds the font's curve and band textures and activates the shader program.
    /// </summary>
    void BindAndSetUniforms(Camera camera);
}
