using Sundex.Engine.Text.Fonts;
using Sundex.Slug;

namespace Sundex.Testing;

public class MockFontProvider : IFontProvider
{
    public SlugFont GetFont(ReadOnlySpan<char> fontName)
    {
        return null!;
    }
}
