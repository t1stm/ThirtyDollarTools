using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace Shared.Renderer.Planes.Uniforms;

[StructLayout(LayoutKind.Explicit, Size = 416)]
public struct GradientUniform
{
    [FieldOffset(0)] public Matrix4 Model;
    [FieldOffset(64)] public Matrix4 Projection;
    [FieldOffset(128)] public Vector4 ScaleAndBorderPx;
    [FieldOffset(144)] public GradientType GradientType;
    [FieldOffset(148)] public int GradientStopCount;

    [FieldOffset(160)] public Vector4 Color0;
    [FieldOffset(176)] public Vector4 Color1;
    [FieldOffset(192)] public Vector4 Color2;
    [FieldOffset(208)] public Vector4 Color3;
    [FieldOffset(224)] public Vector4 Color4;
    [FieldOffset(240)] public Vector4 Color5;
    [FieldOffset(256)] public Vector4 Color6;
    [FieldOffset(272)] public Vector4 Color7;

    [FieldOffset(288)] public float Stop0;
    [FieldOffset(304)] public float Stop1;
    [FieldOffset(320)] public float Stop2;
    [FieldOffset(336)] public float Stop3;
    [FieldOffset(352)] public float Stop4;
    [FieldOffset(368)] public float Stop5;
    [FieldOffset(384)] public float Stop6;
    [FieldOffset(400)] public float Stop7;
}