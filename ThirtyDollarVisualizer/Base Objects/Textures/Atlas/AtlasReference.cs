using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using ThirtyDollarVisualizer.Objects.Playfield.Atlas;

namespace ThirtyDollarVisualizer.Base_Objects.Textures.Atlas;

public readonly struct AtlasReference(RectangleF coordinates, ImageAtlas container)
{
    public readonly RectangleF Coordinates = coordinates;
    public readonly ImageAtlas Container = container;

    public QuadUV TextureUV => new()
    {
        UV0 = new Vector2(Coordinates.X / Container.Width, Coordinates.Y / Container.Height),
        UV1 = new Vector2((Coordinates.X + Coordinates.Width) / Container.Width, Coordinates.Y / Container.Height), 
        UV2 = new Vector2((Coordinates.X + Coordinates.Width) / Container.Width, (Coordinates.Y + Coordinates.Height) / Container.Height),
        UV3 = new Vector2(Coordinates.X / Container.Width, (Coordinates.Y + Coordinates.Height) / Container.Height)
    };
    
    public float Width => Coordinates.Width;
    public float Height => Coordinates.Height;
}