using System.Globalization;
using OpenTK.Mathematics;

namespace Sundex.Style.DSL.Abstract.Values;

public record ColorValue(string Value) : IStyleValue
{
    public Vector4 Vector { get; } = ParseColorFromHex(Value);
    object IStyleValue.Value => Value;

    public override string ToString()
    {
        return Value;
    }

    internal static Vector4 ParseColorFromHex(ReadOnlySpan<char> hex)
    {
        var hexTrimmed = hex.TrimStart('#');
        if (hexTrimmed.Length is not (6 or 8))
            throw new ArgumentException("Invalid hex color format, expected #RRGGBB(AA)");

        var r = byte.Parse(hexTrimmed[..2], NumberStyles.HexNumber);
        var g = byte.Parse(hexTrimmed[2..4], NumberStyles.HexNumber);
        var b = byte.Parse(hexTrimmed[4..6], NumberStyles.HexNumber);
        byte a = 255;
        if (hexTrimmed.Length == 8)
            a = byte.Parse(hexTrimmed[6..8], NumberStyles.HexNumber);

        return new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f);
    }
}