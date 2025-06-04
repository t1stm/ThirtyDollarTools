using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ThirtyDollarVisualizer.Base_Objects.Textures.Static;

public class IconFlexTexture : StaticTexture
{
    public IconFlexTexture(IReadOnlyCollection<StaticTexture> textures, float gapPx, float fontSize) : base(rgba: null)
    {
        var texture_count = textures.Count;
        var size_w = (int)Math.Ceiling(fontSize);
        const int maxImagesPerLine = 3;

        if (texture_count < 1) return;

        var width = texture_count <= maxImagesPerLine
            ? texture_count * (size_w + gapPx)
            : maxImagesPerLine * (size_w + gapPx);
        var height = (int)Math.Max(size_w,
            Math.Ceiling((float)texture_count / maxImagesPerLine) * (size_w + gapPx));

        Image = new Image<Rgba32>((int)width, height);
        Width = Image.Width;
        Height = Image.Height;

        var x = 0;
        var y = 0;

        var break_w = (int)(140 * fontSize);

        foreach (var t in textures)
        {
            var x1 = x;
            var y1 = y;

            var texture = t.GetData();
            if (texture == null) continue;

            var t_height = (int)(size_w / ((float)texture.Width / texture.Height));
            texture.Mutate(r => r.Resize(size_w, t_height));

            var offset = (size_w - t_height) / 2;

            Image.Mutate(r =>
                r.DrawImage(texture, new Point(x1, y1 + offset), 1f)
            );

            x += (int)(size_w + gapPx);
            if (x + size_w <= width) continue;

            x = 0;
            y += (int)(size_w + gapPx);
            if (y > break_w) break;
        }
    }
}