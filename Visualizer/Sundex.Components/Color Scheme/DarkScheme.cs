using System.Globalization;
using OpenTK.Mathematics;

namespace Sundex.Components.Color_Scheme;

public static class DarkScheme
{
    public static Vector4 BgMain { get; } = ParseHexColor("#1A1B26");
    public static Vector4 BgSurface { get; } = ParseHexColor("#16161E");
    public static Vector4 BgElevated { get; } = ParseHexColor("#2A2E3A");

    public static Vector4 TextPrimary { get; } = ParseHexColor("#D6DADC");
    public static Vector4 TextSecondary { get; } = ParseHexColor("#8F93A2");
    public static Vector4 TextFaint { get; } = ParseHexColor("#6C7086");
    public static Vector4 TextHint { get; } = ParseHexColor("#4C4F60");

    public static Vector4 AccentBlue { get; } = ParseHexColor("#7AA2F7");
    public static Vector4 BlueLight { get; } = ParseHexColor("#9BC0FF");
    public static Vector4 BlueDark { get; } = ParseHexColor("#4C78A8");

    public static Vector4 AccentTeal { get; } = ParseHexColor("#2BBAC9");
    public static Vector4 AccentGreen { get; } = ParseHexColor("#9ECE6A");
    public static Vector4 AccentOrange { get; } = ParseHexColor("#FF966C");
    public static Vector4 AccentMagenta { get; } = ParseHexColor("#C792EA");

    public static Vector4 ColorSuccess { get; } = ParseHexColor("#9ECE6A");
    public static Vector4 ColorWarning { get; } = ParseHexColor("#EBCB8B");
    public static Vector4 ColorError { get; } = ParseHexColor("#F7768E");
    public static Vector4 ColorInfo { get; } = ParseHexColor("#7AA2F7");

    private static Vector4 ParseHexColor(string hex) => new(
        int.Parse(hex.Substring(1, 2), NumberStyles.HexNumber) / 255f,
        int.Parse(hex.Substring(3, 2), NumberStyles.HexNumber) / 255f,
        int.Parse(hex.Substring(5, 2), NumberStyles.HexNumber) / 255f, 
        1f);
}