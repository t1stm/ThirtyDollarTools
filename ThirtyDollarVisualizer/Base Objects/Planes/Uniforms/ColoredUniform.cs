using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects.Planes.Uniforms;

[StructLayout(LayoutKind.Explicit, Size = 160)]
public struct ColoredUniform
{
    [FieldOffset(0)] public Matrix4 Model;
    [FieldOffset(64)] public Matrix4 Projection;
    [FieldOffset(128)] public Vector4 Color;
    [FieldOffset(144)] public float ScalePx;            // 4 bytes
    [FieldOffset(148)] public float AspectRatio;        // 4 bytes
    [FieldOffset(152)] public float BorderRadiusPx;     // 4 bytes
}