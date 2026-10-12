using Sundex.Slug;

namespace Sundex.Engine.Text.Fonts;

/// <summary>
///     Abstraction over <see cref="FontProvider" /> to allow mock implementations for testing.
/// </summary>
public interface IFontProvider
{
    /// <summary>Returns the loaded <see cref="SlugFont" /> for the named font.</summary>
    SlugFont GetFont(ReadOnlySpan<char> fontName);
}
