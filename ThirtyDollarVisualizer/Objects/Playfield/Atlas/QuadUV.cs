using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using ThirtyDollarVisualizer.Engine.Renderer;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Objects.Playfield.Atlas;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct QuadUV : IGPUReflection
{
    public Vector4 UV;

    public override string ToString()
    {
        return $"{UV}";
    }

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        layout.PushFloat(2, true); // QuadUV.UV0
        layout.PushFloat(2, true); // QuadUV.UV1
    }

    public static QuadUV FromRectangle(RectangleF reference, int atlasWidth, int atlasHeight)
    {
        return new QuadUV
        {
            UV = (reference.X / atlasWidth, reference.Y / atlasHeight, (reference.X + reference.Width) / atlasWidth,
                (reference.Y + reference.Height) / atlasHeight),
        };
    }
}