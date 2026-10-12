using Sundex.Engine.Text.Fonts;
using Sundex.Slug;

namespace Sundex.Components.Tests;

public class MockFontProvider : IFontProvider
{
    public SlugFont GetFont(ReadOnlySpan<char> fontName)
    {
        return null!;
    }
}
