using OpenTK.Mathematics;
using System.Runtime.InteropServices;
using ThirtyDollarVisualizer.Engine.Renderer;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;

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