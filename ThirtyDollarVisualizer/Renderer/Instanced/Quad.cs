using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Renderer.Instanced;

[StructLayout(LayoutKind.Explicit, Size = 84)]
public struct Quad
{
    [FieldOffset(0)]
    public Matrix4 Model;
    [FieldOffset(64)]
    public Vector4 Color;
    [FieldOffset(80)]
    public uint TextureIndex;
}