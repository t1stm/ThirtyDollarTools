using System;

namespace ThirtyDollarConverter.Resamplers;

public class ByteCruncherResampler : IResampler
{
    private readonly float bits_per_sample;
    public ByteCruncherResampler(float bits_per_sample = 64f)
    {
        this.bits_per_sample = bits_per_sample;
    }
    
    public float[] Resample(float[] samples, uint sampleRate, uint targetSampleRate)
    {
        var increment = (float) targetSampleRate / sampleRate;
        
        var resampled_size = (ulong) Math.Ceiling((double) increment * samples.Length);
        var resampled = new float[resampled_size];

        for (ulong i = 0; i < resampled_size; i++)
        {
            var current_index = Math.Floor(i / increment);
            var current_sample = samples[(ulong) Math.Clamp(current_index, 0, samples.Length - 1)];
            
            var crunched = (int) (current_sample * bits_per_sample);
            resampled[i] = crunched / bits_per_sample;
        }

        return resampled;
    }
}