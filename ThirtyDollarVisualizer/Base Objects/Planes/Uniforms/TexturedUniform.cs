using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects.Planes.Uniforms;

[StructLayout(LayoutKind.Explicit, Size = 144)]
public struct TexturedUniform
{
    [FieldOffset(0)] public Matrix4 Model;
    [FieldOffset(64)] public Matrix4 Projection;
    [FieldOffset(128)] public float DeltaAlpha;
}