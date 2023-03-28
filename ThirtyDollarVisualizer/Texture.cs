using System.Drawing;

namespace ThirtyDollarVisualizer;

public class Texture
{
    public Texture(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException(
            $"Texture file for path \'{path}\' not found. Working directory is \'{Directory.GetCurrentDirectory()}\'.");
        var image = Image.FromFile(path);
        
    }
}