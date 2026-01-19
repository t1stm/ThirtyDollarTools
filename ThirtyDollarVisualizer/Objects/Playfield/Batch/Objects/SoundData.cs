using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Engine.Renderer;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SoundData : IGPUReflection
{
    public Matrix4 Model;
    public Vector4 RGBA;

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