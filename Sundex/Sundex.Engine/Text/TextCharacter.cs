using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Abstract;

namespace Sundex.Engine.Text;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TextCharacter() : IGPUReflection, IPositionable
{
    public Vector4 TextureUV;
    public Vector3 Position { get; set; }
    public Vector3 Scale { get; set; }
    public Vector4 Color { get; set; } = Vector4.One;

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        layout.PushFloat(4, true);
        layout.PushFloat(3, true);
        layout.PushFloat(3, true);
        layout.PushFloat(4, true);
    }
}