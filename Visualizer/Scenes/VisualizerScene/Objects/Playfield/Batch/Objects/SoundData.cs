using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Abstract;

namespace VisualizerScene.Objects.Playfield.Batch.Objects;

/// <summary>One sound quad: 24 bytes. A new one is a 1x1 white quad at the origin.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SoundData() : IGPUReflection
{
    /// <summary>
    ///     The quad's top-left corner (xy) and its width and height (zw). Sounds only ever move and
    ///     scale, never rotate, so this stands in for a model matrix at a quarter of its size.
    /// </summary>
    public Vector4 Box = (0, 0, 1, 1);

    private uint _rgba = PackedColor.White;

    public float OffsetPercentage;

    /// <summary>Stored at 8 bits per channel; see <see cref="PackedColor" />.</summary>
    public Vector4 RGBA
    {
        readonly get => PackedColor.Unpack(_rgba);
        set => _rgba = PackedColor.Pack(value);
    }

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        layout.PushFloat(4, true); // Box
        layout.PushByte(4, true, normalized: true); // RGBA
        layout.PushFloat(1, true); // OffsetPercentage
    }

    public override string ToString()
    {
        return $"Box: {{{Box}}} RGBA: {{{RGBA}}}";
    }
}
