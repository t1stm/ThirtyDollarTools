namespace ThirtyDollarEncoder.PCM;

public static class Int24Extensions
{
    public static float ToFloat(this Int24 int24)
    {
        // I sometimes wonder how I can sit there for two whole hours making this the most complicated code I've written, and then just say "Oh."
        // And write this.
        return ((int24.b1 << 8) | (int24.b2 << 16) | (int24.b3 << 24)) / (float)int.MaxValue;
    }

    // TODO: I am sure that this method doesn't work, but I won't bother testing it, because it isn't needed.
    public static void Set(this Int24 int24, int value)
    {
        if (value is > Int24.MaxValue or < Int24.MinValue)
            throw new ArgumentOutOfRangeException(nameof(value),
                "The supplied value to set is bigger or smaller than the Int24 limit.");
        int24.b1 = (byte)(value << 16);
        int24.b2 = (byte)(value << 8);
        int24.b3 = (byte)value;
    }
}