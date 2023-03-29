using System.Drawing;

namespace ThirtyDollarVisualizer;

public class Texture
{
    private Color[][] Pixels;
    public Texture(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException(
            $"Texture file for path \'{path}\' not found. Working directory is \'{Directory.GetCurrentDirectory()}\'.");
        var image = Image.FromFile(path);
        var bitmap = new Bitmap(image);
        
        var height = bitmap.Height;
        var width = bitmap.Width;
        var pixels = new Color[height][];
        
        for (var x = 0; x < height; x++)
        {
            pixels[x] = new Color[width];
            for (var y = 0; y < width; y++)
            {
                pixels[x][y] = bitmap.GetPixel(x, y);
            }
        }
        
        bitmap.Dispose();
        image.Dispose();

        Pixels = pixels;
    }

    public void Render()
    {
        // TODO: Implement behavior
    }
}