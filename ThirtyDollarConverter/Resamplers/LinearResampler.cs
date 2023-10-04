using System;

namespace ThirtyDollarConverter.Resamplers;

public class LinearResampler : IResampler
{
    public float[] Resample(Span<float> samples, uint sample_rate, uint target_sample_rate)
    {
        var old_size = samples.Length;
        var duration_secs = (float)old_size / sample_rate;
        var new_size = (int)(duration_secs * target_sample_rate);

        var resampled = new float[new_size];

        for (var i = 0; i < new_size; i++)
        {
            var timeSecs = (float) i / target_sample_rate;
            var index = (int) (timeSecs * sample_rate);

            var frac = timeSecs * sample_rate - index;
            if (index < old_size - 1)
                resampled[i] = samples[index] * (1 - frac) + samples[index + 1] * frac;
            else
                resampled[i] = samples[index];
        }

        return resampled;
    }
    
    public double[] Resample(Span<double> samples, uint sample_rate, uint target_sample_rate)
    {
        var old_size = samples.Length;
        var duration_secs = (double)old_size / sample_rate;
        var new_size = (int)(duration_secs * target_sample_rate);

        var resampled = new double[new_size];

        for (var i = 0; i < new_size; i++)
        {
            var timeSecs = (double) i / target_sample_rate;
            var index = (int) (timeSecs * sample_rate);

            var frac = timeSecs * sample_rate - index;
            if (index < old_size - 1)
                resampled[i] = samples[index] * (1 - frac) + samples[index + 1] * frac;
            else
                resampled[i] = samples[index];
        }

        return resampled;
    }
}