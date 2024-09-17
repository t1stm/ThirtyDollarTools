using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Helpers.Decoders;

public static class BackgroundParser
{
    public static (Vector4 parsed_color, float seconds) ParseFromDouble(double value)
    {
        var parsed_value = (long)value;

        var r = (byte)parsed_value;
        var g = (byte)(parsed_value >> 8);
        var b = (byte)(parsed_value >> 16);
        var a = (byte)(parsed_value >> 24);
        var color = new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);

        var seconds = (parsed_value >> 32) / 1000f;

        return (color, seconds);
    }
}