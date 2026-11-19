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

        var red = byte.Parse(hexTrimmed[..2], NumberStyles.HexNumber);
        var green = byte.Parse(hexTrimmed[2..4], NumberStyles.HexNumber);
        var blue = byte.Parse(hexTrimmed[4..6], NumberStyles.HexNumber);
        byte alpha = 255;
        if (hexTrimmed.Length == 8)
            alpha = byte.Parse(hexTrimmed[6..8], NumberStyles.HexNumber);

        return new Vector4(red / 255.0f, green / 255.0f, blue / 255.0f, alpha / 255.0f);
    }
}