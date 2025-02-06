using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ThirtyDollarVisualizer.Objects.Textures.Static;

public class IconFlexTexture : StaticTexture
{
    public IconFlexTexture(IReadOnlyCollection<StaticTexture> textures, float gap_px, float font_size): base(rgba: null)
    {
        var texture_count = textures.Count;
        var size_w = (int)Math.Ceiling(font_size);
        const int max_images_per_line = 3;

        if (texture_count < 1)
        {
            throw new ArgumentException("Texture count must be greater than 0.");
        }

        var width = texture_count <= max_images_per_line
            ? texture_count * (size_w + gap_px)
            : max_images_per_line * (size_w + gap_px);
        var height = (int)Math.Max(size_w,
            Math.Ceiling((float)texture_count / max_images_per_line) * (size_w + gap_px));
        
        image = new Image<Rgba32>((int)width, height);
        Width = image.Width;
        Height = image.Height;
        
        var x = 0;
        var y = 0;

        var break_w = (int)(140 * font_size);

        foreach (var t in textures)
        {
            var x1 = x;
            var y1 = y;

            var texture = t.GetData();
            if (texture == null) continue;

            var t_height = (int)(size_w / ((float)texture.Width / texture.Height));
            texture.Mutate(r => r.Resize(size_w, t_height));

            var offset = (size_w - t_height) / 2;

            image.Mutate(r =>
                r.DrawImage(texture, new Point(x1, y1 + offset), 1f)
            );

            x += (int)(size_w + gap_px);
            if (x + size_w <= width) continue;

            x = 0;
            y += (int)(size_w + gap_px);
            if (y > break_w) break;
        }
    }
}