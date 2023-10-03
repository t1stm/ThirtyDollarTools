using System;

namespace ThirtyDollarConverter.Resamplers;

public class NoInterpolationResampler : IResampler
{
    public float[] Resample(float[] samples, uint sampleRate, uint targetSampleRate)
    {
        var increment = (float) targetSampleRate / sampleRate;
        
        var resampled_size = (ulong) Math.Ceiling((double) increment * samples.Length);
        var resampled = new float[resampled_size];

        for (ulong i = 0; i < resampled_size; i++)
        {
            var current_index = Math.Floor(i / increment);
            var current_sample = samples[(ulong) Math.Clamp(current_index, 0, samples.Length - 1)];
            
            resampled[i] = current_sample;
        }

        return resampled;
    }
}