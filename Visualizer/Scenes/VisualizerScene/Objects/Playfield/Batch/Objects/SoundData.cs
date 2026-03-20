using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Abstract;

namespace VisualizerScene.Objects.Playfield.Batch.Objects;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SoundData : IGPUReflection
{
    public Matrix4 Model;
    public Vector4 RGBA;

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        layout.PushMatrix4(1); // Model
        layout.PushFloat(4, true); // RGBA
    }

    public override string ToString()
    {
        var modelString = Model.ToString();
        modelString = modelString.Replace('\n', ' ');

        return $"Model: {{{modelString}}} RGBA: {{{RGBA}}}";
    }
}