using System.Runtime.InteropServices;
using ThirtyDollarVisualizer.Engine.Renderer;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Objects.Playfield.Atlas;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct StaticSound : IGPUReflection
{
    public SoundData Data;
    public QuadUV TextureUV;

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        SoundData.SelfReflectToGL(layout);
        QuadUV.SelfReflectToGL(layout);
    }

    public override string ToString()
    {
        return $"Data: {{{Data.ToString()}}} Texture UV: {{{TextureUV.ToString()}}}";
    }
}