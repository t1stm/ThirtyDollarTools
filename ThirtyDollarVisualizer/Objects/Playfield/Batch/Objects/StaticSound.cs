using System.Runtime.InteropServices;
using ThirtyDollarVisualizer.Objects.Playfield.Atlas;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;

[StructLayout(LayoutKind.Explicit, Size = 96)]
public struct StaticSound
{
    [FieldOffset(0)] public SoundData Data;
    [FieldOffset(80)] public QuadUV TextureUV;
}