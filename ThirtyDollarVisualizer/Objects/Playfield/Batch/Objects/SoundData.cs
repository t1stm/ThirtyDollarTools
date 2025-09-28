using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Renderer;
using ThirtyDollarVisualizer.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;

[StructLayout(LayoutKind.Explicit, Size = 80)]
public struct SoundData : IGLReflection
{
    [FieldOffset(0)] public Matrix4 Model;
    [FieldOffset(64)] public Vector4 InverseRGBA;

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        layout.PushMatrix4(1); // Model
        layout.PushFloat(4, true); // InverseRGBA
    }
}