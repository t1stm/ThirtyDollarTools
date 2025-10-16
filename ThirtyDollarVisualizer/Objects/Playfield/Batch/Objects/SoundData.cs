using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Renderer;
using ThirtyDollarVisualizer.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;

[StructLayout(LayoutKind.Explicit, Size = 80)]
public struct SoundData : IGLReflection, IDebugStringify
{
    [FieldOffset(0)] public Matrix4 Model;
    [FieldOffset(64)] public Vector4 RGBA;

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        layout.PushMatrix4(1); // Model
        layout.PushFloat(4, true); // InverseRGBA
    }

    public override string ToString()
    {
        var modelString = Model.ToString();
        modelString = modelString.Replace('\n', ' ');
        
        return $"Model: {{{modelString}}} InverseRGBA: {{{RGBA}}}";
    }
}