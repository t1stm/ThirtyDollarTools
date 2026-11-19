using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace Sundex.Components.Renderer.Planes.Uniforms;

[StructLayout(LayoutKind.Explicit, Size = 176)]
public struct TexturedUniform
{
    [FieldOffset(0)] public Matrix4 Model;
    [FieldOffset(64)] public Matrix4 Projection;
    [FieldOffset(128)] public Vector4 ScaleAndBorderPx;

    /// <summary>Multiplied over the sampled texel: white draws the texture as-is.</summary>
    [FieldOffset(144)] public Vector4 Color;

    /// <summary>The part of the texture drawn: offset x, offset y, width, height, in 0–1.</summary>
    [FieldOffset(160)] public Vector4 UvRect;
}
