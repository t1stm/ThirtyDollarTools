using Sundex.MSDF;

namespace Sundex.Engine.Text.Fonts;

/// <summary>
///     Abstraction over <see cref="FontProvider" /> to allow mock implementations for testing.
/// </summary>
public interface IFontProvider
{
    /// <summary>Returns the loaded <see cref="MsdfFont" /> for the named font.</summary>
    MsdfFont GetFont(ReadOnlySpan<char> fontName);
}
