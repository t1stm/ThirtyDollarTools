using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarEncoder.DPCM;

public class DPCMDecoder
{
    public short[]? DecodeToPcm(IEnumerable<byte> data, PcmDataHolder dataHolder)
    {
        var audio = data.ToArray(); // I hope that it's audio.

        return null;
    }
}