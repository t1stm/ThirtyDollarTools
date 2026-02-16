using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Abstract;

namespace VisualizerScene.Objects.Playfield.Batch.Objects;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BackgroundBlip : IGPUReflection
{
    public Matrix4 Model;
    public Vector4 Color;

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        layout.PushMatrix4(1);
        layout.PushFloat(4, true);
    }
}