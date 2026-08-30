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

    /// <summary>
    ///     Per-glyph scissor, in the same absolute UI units as <see cref="Position" />:
    ///     (left, top, right, bottom). All-zero means unclipped, which is what a default
    ///     <see cref="TextCharacter" /> - and so every caller that never sets one - gets.
    ///     Per glyph rather than a per-draw uniform because a whole <see cref="TextBuffer" />
    ///     is one instanced draw call, and glyphs sharing it can need different clip boxes.
    /// </summary>
    public Vector4 ClipRect { get; set; }

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        layout.PushFloat(4, true);
        layout.PushFloat(3, true);
        layout.PushFloat(3, true);
        layout.PushFloat(4, true);
        layout.PushFloat(4, true);
    }
}