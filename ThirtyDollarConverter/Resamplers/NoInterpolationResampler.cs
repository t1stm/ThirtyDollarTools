using System;

namespace ThirtyDollarConverter.Resamplers;

public class NoInterpolationResampler : IResampler
{
    public float[] Resample(Memory<float> samples, uint sample_rate, uint target_sample_rate)
    {
        var span = samples.Span;
        var increment = (float) target_sample_rate / sample_rate;
        
        var resampled_size = (ulong) Math.Ceiling((double) increment * samples.Length);
        var resampled = new float[resampled_size];

        for (ulong i = 0; i < resampled_size; i++)
        {
            var current_index = Math.Floor(i / increment);
            var current_sample = span[(int) Math.Clamp(current_index, 0, samples.Length - 1)];
            
            resampled[i] = current_sample;
        }

        return resampled;
    }
    
    public double[] Resample(Memory<double> samples, uint sample_rate, uint target_sample_rate)
    {
        var span = samples.Span;
        var increment = (double) target_sample_rate / sample_rate;
        
        var resampled_size = (ulong) Math.Ceiling(increment * samples.Length);
        var resampled = new double[resampled_size];

        for (ulong i = 0; i < resampled_size; i++)
        {
            var current_index = Math.Floor(i / increment);
            var current_sample = span[(int) Math.Clamp(current_index, 0, samples.Length - 1)];
            
            resampled[i] = current_sample;
        }

        return resampled;
    }
}