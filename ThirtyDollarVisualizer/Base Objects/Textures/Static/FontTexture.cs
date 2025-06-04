using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ThirtyDollarVisualizer.Base_Objects.Text;

namespace ThirtyDollarVisualizer.Base_Objects.Textures.Static;

public class FontTexture : StaticTexture
{
    public FontTexture(Font font, string text, Color? color = null) : base(rgba: null)
    {
        var options = new TextOptions(font)
        {
            FallbackFontFamilies =
            [
                Fonts.GetEmojiFamily()
            ]
        };
        var rect = TextMeasurer.MeasureAdvance(text, options);

        var c_w = Math.Ceiling(rect.Width);
        // magic number because imagesharp broke the MeasureSize method.
        var c_h = Math.Ceiling(rect.Height + 7);

        var texture_data = new Image<Rgba32>((int)Math.Max(c_w, 1), (int)Math.Max(c_h, 1), Color.Transparent);
        Width = texture_data.Width;
        Height = texture_data.Height;

        var point = PointF.Empty;

        color ??= Color.White;

        var cast_color = color.Value;

        texture_data.Mutate(x =>
            x.DrawText(text, font, Color.Black, point)
                .GaussianBlur(1f)
                .DrawText(text, font, cast_color, point)
        );

        Image = texture_data;
    }
}