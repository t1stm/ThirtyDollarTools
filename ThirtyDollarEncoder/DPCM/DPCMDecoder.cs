using System.Runtime.InteropServices;
using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarEncoder.DPCM;

public static class DPCMDecoder
{
    public static PcmDataHolder DecodeToPcm(IEnumerable<byte> data)
    {
       var bytes = data as byte[] ?? data.ToArray();
        //short sample = 1;
        
        var data_holder = new PcmDataHolder
        {
            Encoding = Encoding.Int8,
            Channels = 1
        };

        var audio_data = new Queue<short>();
        short previous_sample = 0;

        foreach (var dpcm_sample in bytes)
        {
            var dpcmSample = (short)((dpcm_sample << 8) >> 8);
            var pcmSample = (short) (previous_sample + dpcmSample);
            
            audio_data.Enqueue(pcmSample);
            previous_sample = pcmSample;
        }

        data_holder.SampleRate = 48000;
        data_holder.AudioData = MemoryMarshal.Cast<short, byte>(audio_data.ToArray()).ToArray();
        
        return data_holder;
    }
}