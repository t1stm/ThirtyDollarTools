namespace ThirtyDollarEncoder.DPCM;

// This class is a meme / shitpost.
public static class DPCMEncoder
{
    public static byte[] Encode(ReadOnlySpan<float> data)
    {
        return Encode(data.ToArray().Select(b => (short)(b * 32768)).ToArray());
    }

    public static byte[] Encode(ReadOnlySpan<short> data)
    {
        const byte step_interval = 8;
        
        var audio = data.ToArray();
        short old_sample = 0;

        var byte_position = 0;
        
        var output = new byte[audio.LongLength];
        for (long i = 0; i < audio.LongLength; i++, 
             byte_position = byte_position + 1 >= 8 ? 0 : byte_position + 1)
        {
            // TODO: Fix
            short sample = output[i];
            var delta_sample = (short) (sample - old_sample);

            var is_positive = delta_sample << 7 == 1;
            
            output[i] = (byte) (delta_sample << byte_position);
            old_sample = (short) (sample + delta_sample);
        }
        
        return output;
    }
}