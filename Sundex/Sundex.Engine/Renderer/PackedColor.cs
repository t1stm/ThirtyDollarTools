using OpenTK.Mathematics;

namespace Sundex.Engine.Renderer;

/// <summary>
///     A 0..1 color as four bytes, red in the lowest: what a <c>vec4</c> attribute pushed with
///     <see cref="VertexBufferLayout.PushByte" /> as normalized reads back. A quarter of a
///     <see cref="Vector4" />, at the 8 bits per channel the framebuffer keeps anyway.
/// </summary>
public static class PackedColor
{
    public const uint White = uint.MaxValue;

    public static uint Pack(Vector4 color)
    {
        return Channel(color.X) | Channel(color.Y) << 8 | Channel(color.Z) << 16 | Channel(color.W) << 24;
    }

    public static Vector4 Unpack(uint color)
    {
        return new Vector4(color & 0xFF, color >> 8 & 0xFF, color >> 16 & 0xFF, color >> 24) / 255f;
    }

    private static uint Channel(float value)
    {
        return (uint)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);
    }
}
