namespace ThirtyDollarConverter.Audio.Resamplers;

public class LinearResampler : IResampler
{
    public float[] Resample(Memory<float> samples, uint sample_rate, uint target_sample_rate)
    {
        var span = samples.Span;
        var old_size = samples.Length;
        var duration_secs = (float)old_size / sample_rate;
        var new_size = (int)(duration_secs * target_sample_rate);

        var resampled = new float[new_size];

        for (var i = 0; i < new_size; i++)
        {
            var timeSecs = (float)i / target_sample_rate;
            var index = (int)(timeSecs * sample_rate);

            var frac = timeSecs * sample_rate - index;
            if (index < old_size - 1)
                resampled[i] = span[index] * (1 - frac) + span[index + 1] * frac;
            else
                resampled[i] = span[index];
        }

        return resampled;
    }

    public double[] Resample(Memory<double> samples, uint sample_rate, uint target_sample_rate)
    {
        var old_size = samples.Length;
        var span = samples.Span;
        var duration_secs = (double)old_size / sample_rate;
        var new_size = (int)(duration_secs * target_sample_rate);

        var resampled = new double[new_size];

        for (var i = 0; i < new_size; i++)
        {
            var timeSecs = (double)i / target_sample_rate;
            var index = (int)(timeSecs * sample_rate);

            var frac = timeSecs * sample_rate - index;
            if (index < old_size - 1)
                resampled[i] = span[index] * (1 - frac) + span[index + 1] * frac;
            else
                resampled[i] = span[index];
        }

        return resampled;
    }
}