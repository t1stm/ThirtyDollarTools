using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Engine.Renderer;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Engine.Text;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TextCharacter : IGPUReflection
{
    public Vector4 TextureUV;
    public Vector3 Position;
    public Vector2 Scale;
    
    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        layout.PushFloat(4, true);
        layout.PushFloat(3, true);
        layout.PushFloat(2, true);
    }
}