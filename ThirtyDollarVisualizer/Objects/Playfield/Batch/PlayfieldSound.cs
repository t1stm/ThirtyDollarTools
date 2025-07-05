using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch;

[StructLayout(LayoutKind.Explicit, Size = 76)]
public struct PlayfieldSound
{
    [FieldOffset(0)] public Matrix4 Model;
    [FieldOffset(64)] public Vector2 UV;
    [FieldOffset(72)] public float Alpha;
}