using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Abstract;

namespace Sundex.Engine.Text;

/// <summary>
///     One glyph instance: 44 bytes. What is constant per glyph (its em box, banding and band
///     location, 48 bytes) is not carried here but read by the shader from the font's glyph
///     table, at <see cref="Glyph" />.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TextCharacter() : IGPUReflection, IPositionable
{
    // Declaration order is memory order, which SelfReflectToGL mirrors.
    public Vector3 Position { get; set; }

    private Vector2 _scale;
    private uint _color = PackedColor.White;

    /// <summary>The quad's width and height. Z is always 1: the shader has no use for it, so it isn't stored.</summary>
    public Vector3 Scale
    {
        get => new(_scale.X, _scale.Y, 1);
        set => _scale = value.Xy;
    }

    /// <summary>Stored at 8 bits per channel; see <see cref="PackedColor" />.</summary>
    public Vector4 Color
    {
        get => PackedColor.Unpack(_color);
        set => _color = PackedColor.Pack(value);
    }

    /// <summary>
    ///     Per-glyph scissor, in the same absolute UI units as <see cref="Position" />:
    ///     (left, top, right, bottom). All-zero means unclipped, which is what a default
    ///     <see cref="TextCharacter" /> - and so every caller that never sets one - gets.
    ///     Per glyph rather than a per-draw uniform because a whole <see cref="TextBuffer" />
    ///     is one instanced draw call, and glyphs sharing it can need different clip boxes.
    /// </summary>
    public Vector4 ClipRect { get; set; }

    /// <summary>The glyph's row in its font's glyph table: <c>SlugGlyph.Index</c>.</summary>
    public uint Glyph;

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        layout.PushFloat(3, true); // Position
        layout.PushFloat(2, true); // Scale
        layout.PushByte(4, true, normalized: true); // Color
        layout.PushFloat(4, true); // ClipRect
        layout.PushUInt(1, true); // Glyph
    }
}
