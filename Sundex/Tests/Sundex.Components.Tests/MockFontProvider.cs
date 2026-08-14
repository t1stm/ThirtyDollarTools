using Sundex.Engine.Text.Fonts;
using Sundex.MSDF;

namespace Sundex.Components.Tests;

public class MockFontProvider : IFontProvider
{
    public MsdfFont GetFont(ReadOnlySpan<char> fontName)
    {
        return null!;
    }
}
