using System.Runtime.InteropServices;

namespace ThirtyDollarConverter.Encoder.PCM;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Int24
{
    public const int MaxValue = 8388607;
    public const int MinValue = -8388608;
    public byte b1, b2, b3;
}