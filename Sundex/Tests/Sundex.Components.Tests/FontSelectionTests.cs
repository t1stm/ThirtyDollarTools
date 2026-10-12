using System.Numerics;
using System.Reflection;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine.Text;
using Sundex.Engine.Text.Fonts;
using Sundex.Slug;
using Sundex.Slug.Fonts;

namespace Sundex.Components.Tests;

public class FontSelectionTests
{
    /// <summary>Shaped like Lucide: one-em-wide glyphs, ascender 1, descender 0, and a line gap.</summary>
    private sealed class IconGlyphProvider : IGlyphProvider
    {
        private const float P = SlugFontData.QuadPadding;

        public SlugGlyph GetSlugGlyph(ReadOnlySpan<char> character) =>
            new(1f, new Vector4(0.1f - P, 0.1f - P, 0.9f + P, 0.9f + P), Vector4.Zero, Vector4.Zero);

        public SlugFontMetrics GetFontMetrics() => new(1, 1, 0, 1.09, -0.1, 0.05);
    }

    private static (TestUIContext Context, ITextProvider Icons) ContextWithIconFont()
    {
        var context = new TestUIContext();
        var icons = new MockTextProvider(new IconGlyphProvider());
        UIContext.InjectForTesting(context.AssetProvider, context.FontProvider, context.TextProvider,
            new Dictionary<string, ITextProvider> { [Icons.Font] = icons });
        return (context, icons);
    }

    private static ITextProvider ProviderOf(Label label) =>
        ((TextBuffer)typeof(Label).GetField("TextBuffer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(label)!).TextProvider;

    [Fact]
    public void LabelWithAFontDrawsWithThatFontsProvider()
    {
        var (context, icons) = ContextWithIconFont();

        var icon = new Label(context, Icons.Play + Icons.X, Icons.Font) { FontSizePx = 16 };

        Assert.Same(icons, ProviderOf(icon));
        Assert.Same(icons, context.GetTextProvider(Icons.Font));
        Assert.Equal(2 * 1f * 16, icon.Width.Value, 3); // two glyphs at 1 em each
    }

    [Fact]
    public void LabelWithoutAFontIsUnchanged()
    {
        var (context, _) = ContextWithIconFont();

        var label = new Label(context, "ab") { FontSizePx = 16 };

        Assert.Same(context.TextProvider, ProviderOf(label));
        Assert.Equal(2 * 0.6f * 16, label.Width.Value, 3);
        Assert.Equal(1.2f * 16, label.Height.Value, 3); // Lato-shaped: no line gap to drop
    }

    [Fact]
    public void IconLabelIsTheEmSquare()
    {
        var (context, _) = ContextWithIconFont();

        var icon = new Label(context, Icons.Copy, Icons.Font) { FontSizePx = 14 };

        Assert.Equal(14, icon.Width.Value, 3);
        Assert.Equal(14, icon.Height.Value, 3);
    }

    [Fact]
    public void IconBesideTextSharesTheRowCentre()
    {
        var (context, _) = ContextWithIconFont();
        var icon = new Label(context, Icons.Play, Icons.Font) { FontSizePx = 16 };
        var text = new Label(context, "Preview") { FontSizePx = 16 };
        var row = new FlexPanel(context)
        {
            Direction = LayoutDirection.Horizontal,
            VerticalAlign = Align.Center,
            Spacing = 4,
            Width = LiteralOrComputable.AutoSize,
            Height = LiteralOrComputable.AutoSize,
            Children = [icon, text]
        };

        row.Layout();

        Assert.Equal(text.Computed.Height, row.Computed.Height, 3);
        Assert.Equal(icon.Computed.Width + 4, text.Computed.AbsoluteX - icon.Computed.AbsoluteX, 3);
        Assert.Equal(text.Computed.AbsoluteY + text.Computed.Height / 2,
            icon.Computed.AbsoluteY + icon.Computed.Height / 2, 3);
    }

    [Fact]
    public void ButtonPassesItsFontToItsLabel()
    {
        var (context, icons) = ContextWithIconFont();

        var button = new Button(context, Icons.X, font: Icons.Font);

        Assert.Same(icons, ProviderOf(button.Label));
    }
}
