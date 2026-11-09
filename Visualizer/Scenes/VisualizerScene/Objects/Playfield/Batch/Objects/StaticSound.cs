using System.Runtime.InteropServices;
using Shared.Atlases;
using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Abstract;

namespace VisualizerScene.Objects.Playfield.Batch.Objects;

// 24 bytes of data and a 2-byte index, padded to 28: at 26 every other instance's Box would sit
// off 4-byte alignment, which some drivers fetch through a slow path.
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 28)]
public struct StaticSound : IGPUReflection
{
    public SoundData Data;

    /// <summary>The image's slot in its <see cref="StaticSoundAtlas" />'s UV table.</summary>
    public ushort UVIndex;

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        SoundData.SelfReflectToGL(layout);
        layout.PushUShort(1, true).Pad(2); // UVIndex
    }

    public override string ToString()
    {
        return $"Data: {{{Data.ToString()}}} UV Index: {UVIndex}";
    }
}
