using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ThirtyDollarVisualizer.Base_Objects.Textures.Atlas;

/// <summary>
/// Provides a managed atlas for storing and retrieving named textures.
/// Allows operations such as adding and accessing textures by name, while ensuring efficient packing within the atlas bounds.
/// </summary>
public class ManagedAtlas : ImageAtlas
{
    protected readonly Dictionary<string, Rectangle> NamedTextures = new();

    /// <summary>
    /// Tries to retrieve a named texture from the atlas by its path.
    /// </summary>
    /// <param name="path">The key or identifier of the texture to look for in the atlas.</param>
    /// <param name="reference">
    /// When this method returns, contains the atlas reference with coordinates and container information,
    /// if the texture is found; otherwise, contains the default value of <see cref="AtlasReference"/>.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// true if the texture with the specified path exists in the atlas; otherwise, false.
    /// </returns>
    public bool TryGetNamedTexture(string path, out AtlasReference reference)
    {
        reference = default;
        if (!NamedTextures.TryGetValue(path, out var rect))
            return false;

        reference = new AtlasReference(rect, this);
        return true;
    }


    /// <summary>
    /// Adds a named texture to the atlas if space is available and assigns its position.
    /// </summary>
    /// <param name="path">The unique key or identifier for the texture to be added.</param>
    /// <param name="texture">The texture image to be added to the atlas.</param>
    /// <param name="reference">
    /// When this method returns, contains the atlas reference with coordinates, container information,
    /// and texture UV coordinates if the operation is successful; otherwise, contains the default value
    /// of <see cref="AtlasReference"/>.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// true if the texture was successfully added to the atlas; otherwise, false.
    /// </returns>
    public bool AddNamedTexture(string path, Image<Rgba32> texture, out AtlasReference reference)
    {
        if (TryGetNamedTexture(path, out reference))
            return true;

        if (!Atlas.CanFit(texture.Width, texture.Height))
            return false;

        var position = AddImage(texture);
        reference = new AtlasReference(position, this);
        return NamedTextures.TryAdd(path, position);
    }
}