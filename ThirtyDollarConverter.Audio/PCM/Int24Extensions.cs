namespace ThirtyDollarEncoder.PCM;

public static class Int24Extensions
{
    public static float ToFloat(this Int24 int24)
    {
        var value = int24.b1 | (int24.b2 << 8) | (int24.b3 << 16);
        if ((value & 0x800000) != 0)
        {
            value |= unchecked((int)0xFF000000);
        }
        
        return (float)value / Int24.MaxValue;
    }
}