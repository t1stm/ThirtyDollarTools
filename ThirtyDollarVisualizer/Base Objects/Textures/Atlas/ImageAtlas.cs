using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ThirtyDollarVisualizer.Base_Objects.Textures.Static;

namespace ThirtyDollarVisualizer.Base_Objects.Textures.Atlas;

public class ImageAtlas : StaticTexture
{
    protected readonly GuillotineAtlas Atlas;
    protected readonly List<UploadImage> ImagesToBeUploaded = [];

    private bool _firstUpload = true;

    public ImageAtlas(int width, int height) : base(new Image<Rgba32>(width, height, Color.Transparent))
    {
        Atlas = new GuillotineAtlas(width, height);
    }

    public ImageAtlas() : base(rgba: null)
    {
        var size = 8192;
        Atlas = new GuillotineAtlas(size, size, false);
        Image = new Image<Rgba32>(size, size, Color.Transparent);
        Width = Image.Width;
        Height = Image.Height;
    }

    public Rectangle AddImage(ImageFrame<Rgba32> image)
    {
        var position = Atlas.AddImage(image);
        ImagesToBeUploaded.Add(new UploadImage
        {
            Image = image,
            Area = position
        });
        
        return position;
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public override bool NeedsUploading()
    {
        return _firstUpload || ImagesToBeUploaded.Count > 0;
    }

    public override void UploadToGPU() => UploadToGPU(false);

    public override void UploadToGPU(bool dispose)
    {
        if (!_firstUpload)
        {
            UploadToGPUPartial();
            return;
        }

        _firstUpload = false;
        base.UploadToGPU(true);
    }

    public void UploadToImage()
    {
        ArgumentNullException.ThrowIfNull(Image);
        
        foreach (var upload in ImagesToBeUploaded)
        {
            var x = upload.Area.X;
            var y = upload.Area.Y;

            for (var w = 0; w < upload.Area.Width; w++)
            {
                for (var h = 0; h < upload.Area.Height; h++)
                    Image[x + w, y + h] = upload.Image[w, h];
            }
        }
        
        ImagesToBeUploaded.Clear();
    }
    
    private void UploadToGPUPartial()
    {
        if (_firstUpload)
            throw new InvalidOperationException(
                "UploadToGPUPartial was called before the initial call to UploadToGPU.");

        Bind();

        foreach (var upload in ImagesToBeUploaded)
        {
            upload.Image.ProcessPixelRows(accessor => PartialUpload(accessor, upload.Area));
        }

        ImagesToBeUploaded.Clear();
    }

    private static unsafe void PartialUpload(PixelAccessor<Rgba32> pixelAccessor, Rectangle imageArea)
    {
        for (var y = 0; y < pixelAccessor.Height; y++)
            fixed (void* data = pixelAccessor.GetRowSpan(y))
            {
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, imageArea.X, imageArea.Y + y, imageArea.Width, 1,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte, new IntPtr(data));
            }

        SetParameters();
    }

    public override void Bind(TextureUnit slot = TextureUnit.Texture0)
    {
        if (!Handle.HasValue)
        {
            throw new ArgumentNullException(nameof(Handle),
                "Calling Bind in ImageAtlas while handle is null is not allowed.");
        }

        base.Bind(slot);
    }
}

public record struct UploadImage
{
    public ImageFrame<Rgba32> Image;
    public Rectangle Area;
}