using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;

namespace ThirtyDollarVisualizer.Objects.Playfield.Atlas;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct QuadUV
{
    public Vector2 UV0;
    public Vector2 UV1;
    public Vector2 UV2;
    public Vector2 UV3;

    public override string ToString()
    {
        return $"{UV0} {UV1} {UV2} {UV3}";
    }
}

public static class QuadUVExtensions
{
    public static QuadUV ToUV(this RectangleF rectangle, Vector2 atlasSize)
    {
        return new QuadUV
        {
            UV0 = new Vector2(rectangle.Left / atlasSize.X, rectangle.Bottom / atlasSize.Y),
            UV1 = new Vector2(rectangle.Right / atlasSize.X, rectangle.Bottom / atlasSize.Y),
            UV2 = new Vector2(rectangle.Left / atlasSize.X, rectangle.Top / atlasSize.Y),
            UV3 = new Vector2(rectangle.Right / atlasSize.X, rectangle.Top / atlasSize.Y)
        };
    }
}