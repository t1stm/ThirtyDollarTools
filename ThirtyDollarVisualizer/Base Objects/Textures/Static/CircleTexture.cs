using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ThirtyDollarVisualizer.Helpers.Textures;
using ThirtyDollarVisualizer.Objects.Text;

namespace ThirtyDollarVisualizer.Objects.Textures.Static;

public class CircleTexture : StaticTexture
{
    public CircleTexture(Font font, Color circle_color, string text, Color? text_color = null): base(rgba: null)
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

        text_color ??= Color.White;

        var cast_color = text_color.Value;

        text_texture.Mutate(x =>
            x.DrawText(text, font, Color.Black, text_point)
                .GaussianBlur(1f)
                .DrawText(text, font, cast_color, text_point)
        );

        var fs = (int)font.Size - 2;
        var fs_2 = font.Size * 0.5f;

        var circle_texture = new Image<Rgba32>(fs, fs, circle_color);
        circle_texture.Mutate(x =>
            x.ApplyRoundedCorners(fs_2));

        image = new Image<Rgba32>(circle_texture.Width + 5 + text_texture.Width,
            text_texture.Height, Color.Transparent);
        
        image.Mutate(x => x
            .DrawImage(circle_texture, new Point(1, 1), 1f)
            .Brightness(0.2f)
            .BoxBlur(2)
            .DrawImage(circle_texture, new Point(1, 1), 1f)
            .DrawImage(text_texture, new Point(circle_texture.Width + 6, 0), 1f)
        );

        Width = image.Width;
        Height = image.Height;
    }
}