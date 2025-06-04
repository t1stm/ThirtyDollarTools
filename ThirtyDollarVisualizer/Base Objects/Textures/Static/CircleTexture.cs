using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ThirtyDollarVisualizer.Base_Objects.Text;
using ThirtyDollarVisualizer.Helpers.Textures;

namespace ThirtyDollarVisualizer.Base_Objects.Textures.Static;

public class CircleTexture : StaticTexture
{
    public CircleTexture(Font font, Color circleColor, string text, Color? textColor = null) : base(rgba: null)
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

        var text_texture = new Image<Rgba32>((int)Math.Max(c_w, 1), (int)Math.Max(c_h, 1), Color.Transparent);
        var text_point = PointF.Empty;

        textColor ??= Color.White;

        var cast_color = textColor.Value;

        text_texture.Mutate(x =>
            x.DrawText(text, font, Color.Black, text_point)
                .GaussianBlur(1f)
                .DrawText(text, font, cast_color, text_point)
        );

        var fs = (int)font.Size - 2;
        var fs_2 = font.Size * 0.5f;

        var circle_texture = new Image<Rgba32>(fs, fs, circleColor);
        circle_texture.Mutate(x =>
            x.ApplyRoundedCorners(fs_2));

        Image = new Image<Rgba32>(circle_texture.Width + 5 + text_texture.Width,
            text_texture.Height, Color.Transparent);

        Image.Mutate(x => x
            .DrawImage(circle_texture, new Point(1, 1), 1f)
            .Brightness(0.2f)
            .BoxBlur(2)
            .DrawImage(circle_texture, new Point(1, 1), 1f)
            .DrawImage(text_texture, new Point(circle_texture.Width + 6, 0), 1f)
        );

        Width = Image.Width;
        Height = Image.Height;
    }
}