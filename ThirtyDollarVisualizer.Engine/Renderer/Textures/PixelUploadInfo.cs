using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer.Engine.Renderer.Textures;

public readonly struct PixelUploadInfo(PixelFormat format, PixelType type)
{
    public PixelFormat Format { get; } = format;
    public PixelType Type { get; } = type;
}