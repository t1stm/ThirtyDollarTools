using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Renderer.Instanced;

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct Quad
{
    public Matrix4 Model;
    public Vector4 Color;
    public int TextureIndex;
    private readonly Vector3 _pad;
}