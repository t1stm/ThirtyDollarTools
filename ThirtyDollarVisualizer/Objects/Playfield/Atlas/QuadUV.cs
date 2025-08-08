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
}

public static class QuadUVExtensions
{
    public static QuadUV ToUV(this Rectangle rectangle)
    {
        return new QuadUV
        {
            UV0 = new Vector2(rectangle.Left, rectangle.Bottom),
            UV1 = new Vector2(rectangle.Right, rectangle.Bottom),
            UV2 = new Vector2(rectangle.Left, rectangle.Top),
            UV3 = new Vector2(rectangle.Right, rectangle.Top)
        };
    }
}