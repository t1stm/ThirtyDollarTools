using System.Runtime.InteropServices;
using ThirtyDollarVisualizer.Objects.Playfield.Atlas;
using ThirtyDollarVisualizer.Renderer;
using ThirtyDollarVisualizer.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;

[StructLayout(LayoutKind.Explicit, Size = 112)]
public struct StaticSound : IGLReflection
{
    [FieldOffset(0)] public SoundData Data;
    [FieldOffset(80)] public QuadUV TextureUV;

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        SoundData.SelfReflectToGL(layout);
        layout.PushFloat(2); // QuadUV.UV0
        layout.PushFloat(2); // QuadUV.UV1
        layout.PushFloat(2); // QuadUV.UV2
        layout.PushFloat(2); // QuadUV.UV3
    }
}