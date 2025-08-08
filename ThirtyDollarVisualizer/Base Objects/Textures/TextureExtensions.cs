namespace ThirtyDollarVisualizer.Base_Objects.Textures;

public static class TextureExtensions
{
    public static T As<T>(this SingleTexture texture) where T : SingleTexture
    {
        return texture as T ?? throw new InvalidCastException();
    }
}