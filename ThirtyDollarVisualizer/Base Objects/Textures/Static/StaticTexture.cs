using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ThirtyDollarVisualizer.Assets;

namespace ThirtyDollarVisualizer.Base_Objects.Textures.Static;

public class StaticTexture(Image<Rgba32>? rgba) : SingleTexture
{
    public static readonly StaticTexture TransparentPixel = new(new Image<Rgba32>(1, 1));
    private int? _handle;

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
        return _handle == null;
    }

    public override void UploadToGPU()
    {
        if (Image == null) throw new ArgumentNullException(nameof(Image), "Static Texture asset should not be null.");

        _handle = GL.GenTexture();
        if (_handle == 0)
            throw new Exception("Texture generation wasn't successful.");

        Bind();
        BasicUploadTexture(Image.Frames.RootFrame);
        SetParameters();

        Image.Dispose();
        Image = null;
    }

    public override void Bind(TextureUnit slot = TextureUnit.Texture0)
    {
        if (!_handle.HasValue) return;
        GL.ActiveTexture(slot);
        GL.BindTexture(TextureTarget.Texture2D, _handle.Value);
    }

    public override void Dispose()
    {
        if (_handle.HasValue)
            GL.DeleteTexture(_handle.Value);
        GC.SuppressFinalize(this);
    }
}