using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Abstract;

namespace Shared.Atlases;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct QuadUV : IVertexData
{
    public Vector4 UV;

    public override string ToString()
    {
        return $"{UV}";
    }

    public static void DescribeLayout(VertexBufferLayout layout)
    {
        layout.PushFloat(2); // QuadUV.UV0
        layout.PushFloat(2); // QuadUV.UV1
    }

    public static QuadUV FromRectangle(RectangleF reference, int atlasWidth, int atlasHeight)
    {
        return new QuadUV
        {
            UV = (reference.X / atlasWidth, reference.Y / atlasHeight, (reference.X + reference.Width) / atlasWidth,
                (reference.Y + reference.Height) / atlasHeight)
        };
    }
}