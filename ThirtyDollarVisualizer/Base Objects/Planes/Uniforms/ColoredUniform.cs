using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects.Planes.Uniforms;

[StructLayout(LayoutKind.Explicit, Size = 176)]
public struct ColoredUniform
{
    [FieldOffset(0)]
    public Matrix4 Model;
    
    [FieldOffset(64)]
    public Matrix4 Projection;
    
    [FieldOffset(128)]
    public Vector4 Color;
    
    [FieldOffset(144)]
    public Vector3 PositionPx;
    
    [FieldOffset(160)]
    public Vector3 ScalePx;
    
    [FieldOffset(172)]
    public float BorderRadiusPx;
}