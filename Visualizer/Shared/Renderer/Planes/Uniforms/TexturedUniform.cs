using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace Shared.Renderer.Planes.Uniforms;

[StructLayout(LayoutKind.Explicit, Size = 160)]
public struct TexturedUniform
{
    [FieldOffset(0)] public Matrix4 Model;
    [FieldOffset(64)] public Matrix4 Projection;
    [FieldOffset(128)] public Vector4 ScaleAndBorderPx;

    /// <summary>Multiplied over the sampled texel: white draws the texture as-is.</summary>
    [FieldOffset(144)] public Vector4 Color;
}
