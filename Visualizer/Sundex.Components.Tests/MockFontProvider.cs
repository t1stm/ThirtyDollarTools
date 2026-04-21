using Msdfgen.Extensions;
using Sundex.Engine.Text.Fonts;

namespace Sundex.Components.Tests;

public class MockFontProvider : IFontProvider
{
    public FontHandle GetFont(ReadOnlySpan<char> fontName)
    {
        return null!;
    }
}