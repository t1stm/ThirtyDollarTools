using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ThirtyDollarVisualizer.Assets;

namespace ThirtyDollarVisualizer.Base_Objects.Textures.Static;

public class StaticTexture(Image<Rgba32>? rgba) : SingleTexture
{
    private static readonly Lazy<StaticTexture> EmptyTexture = new(() => new StaticTexture(new Image<Rgba32>(1, 1)));
    public static StaticTexture TransparentPixel => EmptyTexture.Value;
    protected int? Handle;

    protected Image<Rgba32>? Image = rgba;

    public StaticTexture(string path) : this(rgba: null)
    {
        var asset = AssetManager.GetAsset(path);
        using var source = asset.Stream;
        Image = SixLabors.ImageSharp.Image.Load<Rgba32>(source);
    }

    public Image<Rgba32>? GetData()
    {
        return Image;
    }

    public override bool NeedsUploading()
    {
        return Handle == null;
    }

    public override void UploadToGPU(bool dispose)
    {
        if (Image == null) throw new InvalidOperationException("Static Texture asset should not be null.");

        Handle = GL.GenTexture();
        if (Handle == 0)
            throw new Exception("Texture generation wasn't successful.");

        Bind();
        BasicUploadTexture(Image.Frames.RootFrame);
        SetParameters();

        if (!dispose) return;
        
        Image.Dispose();
        Image = null;
    }

    public override void Bind(TextureUnit slot = TextureUnit.Texture0)
    {
        if (!Handle.HasValue)
        {
            throw new ArgumentNullException(nameof(Handle));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(Handle.Value, 1, nameof(Handle));
        GL.ActiveTexture(slot);
        GL.BindTexture(TextureTarget.Texture2D, Handle.Value);
    }

    public override void Dispose()
    {
        if (Handle.HasValue)
            GL.DeleteTexture(Handle.Value);
        GC.SuppressFinalize(this);
    }
}