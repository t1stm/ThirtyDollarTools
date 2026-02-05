using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp.PixelFormats;

namespace ThirtyDollarVisualizer.Engine.Renderer.Textures;

public static class UploadInfoProvider<TPixel> where TPixel : unmanaged, IPixel, IPixel<TPixel>
{
    // This will keep one struct allocated for each TPixel that is used in the application but that isn't a problem.
    // ReSharper disable once StaticMemberInGenericType
    public static PixelUploadInfo UploadInfo { get; } = Resolve();

    // converts between ImageSharp and OpenGL pixel formats and types
    private static PixelUploadInfo Resolve()
    {
        var tPixel = typeof(TPixel);
        var pixelFormat = tPixel switch
        {
            not null when tPixel == typeof(Rgba32) || tPixel == typeof(RgbaVector) ||
                          tPixel == typeof(Rgba64) ||
                          tPixel == typeof(NormalizedByte4) || tPixel == typeof(NormalizedShort4) ||
                          tPixel == typeof(HalfVector4) => PixelFormat.Rgba,
            not null when tPixel == typeof(Rgba1010102) => PixelFormat.RgbaInteger,
            not null when tPixel == typeof(Rgb24) || tPixel == typeof(Rgb48) => PixelFormat.Rgb,
            not null when tPixel == typeof(Bgra32) => PixelFormat.Bgra,
            not null when tPixel == typeof(Bgr24) => PixelFormat.Bgr,
            not null when tPixel == typeof(Rg32) || tPixel == typeof(NormalizedByte2) ||
                          tPixel == typeof(NormalizedShort2) || tPixel == typeof(HalfVector2) => PixelFormat.Rg,
            not null when tPixel == typeof(L8) || tPixel == typeof(L16) ||
                          tPixel == typeof(HalfSingle) => PixelFormat.Red,
            not null when tPixel == typeof(A8) => PixelFormat.Alpha,
            _ => throw new Exception($"Unsupported pixel format: {tPixel}")
        };

        var pixelType = tPixel switch
        {
            not null when tPixel == typeof(NormalizedByte2) ||
                          tPixel == typeof(NormalizedByte4) => PixelType.Byte,
            not null when tPixel == typeof(NormalizedShort2) ||
                          tPixel == typeof(NormalizedShort4) => PixelType.Short,

            not null when tPixel == typeof(L8) || tPixel == typeof(A8) ||
                          tPixel == typeof(Rgba32) || tPixel == typeof(Bgra32) ||
                          tPixel == typeof(Rgb24) || tPixel == typeof(Rg32) ||
                          tPixel == typeof(Bgr24) => PixelType.UnsignedByte,

            not null when tPixel == typeof(L16) || tPixel == typeof(Rgb48) => PixelType
                .UnsignedShort,
            not null when tPixel == typeof(RgbaVector) => PixelType.Float,
            not null when tPixel == typeof(HalfSingle) || tPixel == typeof(HalfVector2) ||
                          tPixel == typeof(HalfVector4) => PixelType.HalfFloat,

            not null when tPixel == typeof(Rgba1010102) => PixelType.UnsignedInt1010102,
            not null when tPixel == typeof(Rgba64) => PixelType.UnsignedShort,

            _ => throw new Exception($"Unsupported pixel format: {tPixel}")
        };

        return new PixelUploadInfo(pixelFormat, pixelType);
    }
}