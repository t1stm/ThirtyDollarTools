using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch;

[StructLayout(LayoutKind.Explicit, Size = 68)]
public struct PlayfieldSound
{
    [FieldOffset(0)] public Matrix4 Model;
    [FieldOffset(64)] public float Alpha;
}