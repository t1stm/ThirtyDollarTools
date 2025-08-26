using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;

[StructLayout(LayoutKind.Explicit, Size = 80)]
public struct SoundData
{
    [FieldOffset(0)] public Matrix4 Model;
    [FieldOffset(64)] public Vector4 InverseRGBA;
}