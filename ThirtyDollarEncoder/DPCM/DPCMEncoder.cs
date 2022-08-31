using System;
using System.Collections.Generic;
using System.Linq;
using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarEncoder.DPCM
{
    // This class is a meme / shitpost.
    public class DPCMEncoder
    {
        public byte[]? Encode(IEnumerable<float> data)
        {
            return Encode(data.Select(b => (short) (b * 32768)));
        }
        public byte[]? Encode(IEnumerable<short> data)
        {
            var audio = data.ToArray();
            short oldSample = 0;
            ushort placement = 0;
            var output = new byte[(int) (audio.LongLength / 8) + 1];
            for (long i = 0; i < audio.LongLength; i++)
            {
                var j = (int) Math.Floor((decimal) (i * 0.125));
                output[j] |= (byte) (output[j] | (oldSample > audio[i] ? 0 : 1) << placement);
                if (++placement > 8) placement = 0;
                oldSample = audio[i];
            }
            // For now this won't do anything so don't expect too much. I am currently experimenting.
            return null;
        }

        public short[]? DecodeToPcm(IEnumerable<byte> data, PcmDataHolder dataHolder)
        {
            var audio = data.ToArray(); // I hope that it's audio.
            
            return null;
        }
    }
}