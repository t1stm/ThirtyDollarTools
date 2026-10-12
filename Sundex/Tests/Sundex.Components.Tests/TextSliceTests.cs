using Sundex.Engine.Renderer.Queues;
using Sundex.Engine.Text;
using Sundex.Slug;
using NVector4 = System.Numerics.Vector4;
using TKVector4 = OpenTK.Mathematics.Vector4;

namespace Sundex.Components.Tests;

/// <summary>
///     A Slug glyph's quad must be exactly its padded ink box, placed from the pen and the
///     baseline (docs/handover/sundex-slug-plan.md, "Layout mapping"), and the glyph's shader
///     data has to reach the GPU record untouched.
/// </summary>
public class TextSliceTests
{
    private const float P = SlugFontData.QuadPadding;

    [Fact]
    public void EachGlyphQuadIsItsPaddedInkBoxAtThePen()
    {
        const float l = 0.1f, b = -0.2f, r = 0.6f, t = 0.75f, advance = 0.7f, fontSize = 20f;
        var emRect = new NVector4(l - P, b - P, r + P, t + P);
        var banding = new NVector4(1, 2, 3, 4);
        var glyphData = new NVector4(5, 6, 7, 8);

        var provider = new MockTextProvider();
        ((MockGlyphProvider)provider.GlyphProvider).Glyph = new SlugGlyph(advance, emRect, banding, glyphData);
        var buffer = new TextBuffer(provider, new DeleteQueue());

        var slice = buffer.GetTextSlice("ab");
        slice.UpdateManually = true;
        slice.FontSize = fontSize;
        slice.Position = (100, 50, 0);
        slice.UpdateManually = false;
        slice.UpdateCharacters();

        var baseline = 50 + fontSize;
        for (var i = 0; i < 2; i++)
        {
            var pen = 100 + i * advance * fontSize;
            var character = buffer.Characters[i];

            Assert.Equal(pen + (l - P) * fontSize, character.Position.X, 3);
            Assert.Equal(baseline - (t + P) * fontSize, character.Position.Y, 3);
            Assert.Equal((r - l + 2 * P) * fontSize, character.Scale.X, 3);
            Assert.Equal((t - b + 2 * P) * fontSize, character.Scale.Y, 3);

            Assert.Equal(new TKVector4(emRect.X, emRect.Y, emRect.Z, emRect.W), character.EmRect);
            Assert.Equal(new TKVector4(1, 2, 3, 4), character.Banding);
            Assert.Equal(new TKVector4(5, 6, 7, 8), character.GlyphData);
        }
    }
}
