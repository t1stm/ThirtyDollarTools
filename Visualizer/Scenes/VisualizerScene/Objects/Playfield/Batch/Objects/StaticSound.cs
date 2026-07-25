using System.Runtime.InteropServices;
using Shared.Atlases;
using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Abstract;

namespace VisualizerScene.Objects.Playfield.Batch.Objects;

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