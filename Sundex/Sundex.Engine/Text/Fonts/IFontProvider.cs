using Msdfgen.Extensions;

namespace Sundex.Engine.Text.Fonts;

/// <summary>
///     Abstraction over <see cref="FontProvider" /> to allow mock implementations for testing.
/// </summary>
public interface IFontProvider
{
    /// <summary>Returns a <see cref="FontHandle" /> for the named font.</summary>
    FontHandle GetFont(ReadOnlySpan<char> fontName);
}