using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using ThirtyDollarVisualizer.Renderer;
using ThirtyDollarVisualizer.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Objects.Playfield.Atlas;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct QuadUV: IGLReflection
{
    public Vector2 UV0;
    public Vector2 UV1;
    public Vector2 UV2;
    public Vector2 UV3;

    public override string ToString()
    {
        return $"{UV0} {UV1} {UV2} {UV3}";
    }

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        layout.PushFloat(2, true); // QuadUV.UV0
        layout.PushFloat(2, true); // QuadUV.UV1
        layout.PushFloat(2, true); // QuadUV.UV2
        layout.PushFloat(2, true); // QuadUV.UV3
    }
}