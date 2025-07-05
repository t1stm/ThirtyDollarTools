using OpenTK.Mathematics;
using SixLabors.ImageSharp;

namespace ThirtyDollarVisualizer.Base_Objects.Textures.Atlas;

public readonly struct AtlasReference(Rectangle coordinates, ImageAtlas container)
{
    public readonly Rectangle Coordinates = coordinates;
    public readonly ImageAtlas Container = container;

    public Vector2 TextureUV => new(
        (float)Coordinates.X / Container.Width,
        (float)Coordinates.Y / Container.Height);
    
    public float Width => Coordinates.Width;
    public float Height => Coordinates.Height;
}