using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Engine.Renderer;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Engine.Text;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TextCharacter : IGPUReflection, IPositionable
{
    public Vector4 TextureUV;
    public Vector3 Position { get; set; }
    public Vector3 Scale { get; set; }

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        layout.PushFloat(4, true);
        layout.PushFloat(3, true);
        layout.PushFloat(3, true);
    }
}