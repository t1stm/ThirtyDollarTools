using Sundex.Engine.Renderer.Queues;
using Sundex.Engine.Text;
using Sundex.Slug;
using NVector4 = System.Numerics.Vector4;

namespace Sundex.Components.Tests;

/// <summary>
///     A Slug glyph's quad must be exactly its padded ink box, placed from the pen and the
///     baseline (docs/handover/sundex-slug-plan.md, "Layout mapping"), and the GPU record has to
///     point at the glyph's row of shader data.
/// </summary>
public class TextSliceTests
{
    private const float Padding = SlugFontData.QuadPadding;

    [Fact]
    public void EachGlyphQuadIsItsPaddedInkBoxAtThePen()
    {
        const float left = 0.1f, bottom = -0.2f, right = 0.6f, top = 0.75f, advance = 0.7f, fontSize = 20f;
        var emRect = new NVector4(left - Padding, bottom - Padding, right + Padding, top + Padding);
        var banding = new NVector4(1, 2, 3, 4);
        var glyphData = new NVector4(5, 6, 7, 8);

        var provider = new MockTextProvider();
        ((MockGlyphProvider)provider.GlyphProvider).Glyph =
            new SlugGlyph(advance, emRect, banding, glyphData) { Index = 7 };
        var buffer = new TextBuffer(provider, new DeleteQueue());

        var slice = buffer.GetTextSlice("ab");
        slice.UpdateManually = true;
        slice.FontSize = fontSize;
        slice.Position = (100, 50, 0);
        slice.UpdateManually = false;
        slice.UpdateCharacters();

        var baseline = 50 + fontSize;
        for (var index = 0; index < 2; index++)
        {
            var pen = 100 + index * advance * fontSize;
            var character = buffer.Characters[index];

            Assert.Equal(pen + (left - Padding) * fontSize, character.Position.X, 3);
            Assert.Equal(baseline - (top + Padding) * fontSize, character.Position.Y, 3);
            Assert.Equal((right - left + 2 * Padding) * fontSize, character.Scale.X, 3);
            Assert.Equal((top - bottom + 2 * Padding) * fontSize, character.Scale.Y, 3);

            Assert.Equal(7u, character.Glyph);
        }
    }
}
