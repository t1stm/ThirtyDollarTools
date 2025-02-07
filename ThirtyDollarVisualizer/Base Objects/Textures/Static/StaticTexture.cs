using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ThirtyDollarVisualizer.Objects.Textures.Static;

public class StaticTexture(Image<Rgba32>? rgba) : Texture
{
    public static StaticTexture Transparent1x1 = new(new Image<Rgba32>(1,1));
    
    protected Image<Rgba32>? image = rgba;
    private int? handle;

    public Image<Rgba32>? GetData() => image;

    public StaticTexture(string path): this(rgba: null)
    {
        
    }
    
    public override bool NeedsUploading()
    {
        return handle == null;
    }

    public override void UploadToGPU()
    {
        if (image == null) throw new ArgumentNullException(nameof(image), "Static Texture asset should not be null.");

        handle = GL.GenTexture();
        if (handle == 0) 
            throw new Exception("Texture generation wasn't successful.");
        
        Bind();
        BasicUploadTexture(image.Frames.RootFrame);
        SetParameters();
        
        image.Dispose();
        image = null;
    }

    public override void Bind(TextureUnit slot = TextureUnit.Texture0)
    {
        if (!handle.HasValue) return;
        GL.ActiveTexture(slot);
        GL.BindTexture(TextureTarget.Texture2D, handle.Value);
    }

    public override void Dispose()
    {
        if (handle.HasValue)
            GL.DeleteTexture(handle.Value);
        GC.SuppressFinalize(this);
    }
}