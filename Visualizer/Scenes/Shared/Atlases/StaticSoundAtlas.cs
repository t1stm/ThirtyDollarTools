using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using ThirtyDollarVisualizer.Engine.Renderer.Textures.Atlases;

namespace Shared.Atlases;

public class StaticSoundAtlas(int width, int height, InternalFormat internalFormat = InternalFormat.Rgba8)
    : GPUTextureAtlas(width, height, internalFormat)
{
    public Rectangle GetSoundUV(string soundName)
    {
        return GetSoundUV(soundName.AsSpan());
    }

    public Rectangle GetSoundUV(ReadOnlySpan<char> soundName)
    {
        var found = TryGetSound(soundName, out var reference);
        return !found ? new Rectangle() : reference;
    }

    public bool TryGetSound(ReadOnlySpan<char> soundName, out Rectangle reference)
    {
        reference = Atlas.GetImageRectangle(soundName);
        return !reference.IsEmpty;
    }
}