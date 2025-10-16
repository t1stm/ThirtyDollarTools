using System.Runtime.InteropServices;
using ThirtyDollarVisualizer.Objects.Playfield.Atlas;
using ThirtyDollarVisualizer.Renderer;
using ThirtyDollarVisualizer.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;

[StructLayout(LayoutKind.Explicit, Size = 112)]
public struct StaticSound : IGLReflection, IDebugStringify
{
    [FieldOffset(0)] public SoundData Data;
    [FieldOffset(80)] public QuadUV TextureUV;

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        SoundData.SelfReflectToGL(layout);
        layout.PushFloat(2, true); // QuadUV.UV0
        layout.PushFloat(2, true); // QuadUV.UV1
        layout.PushFloat(2, true); // QuadUV.UV2
        layout.PushFloat(2, true); // QuadUV.UV3
    }

    public override string ToString()
    {
        return $"Data: {{{Data.ToString()}}} Texture UV: {{{TextureUV.ToString()}}}";
    }
}