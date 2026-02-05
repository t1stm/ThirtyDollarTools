namespace ThirtyDollarEncoder.Resamplers;

public class LinearResampler : IResampler
{
    public float[] Resample(Memory<float> samples, uint sampleRate, uint targetSampleRate)
    {
        var span = samples.Span;
        var old_size = samples.Length;
        var duration_secs = (float)old_size / sampleRate;
        var new_size = (int)(duration_secs * targetSampleRate);

        var resampled = new float[new_size];

        for (var i = 0; i < new_size; i++)
        {
            var timeSecs = (float)i / targetSampleRate;
            var index = (int)(timeSecs * sampleRate);

            var frac = timeSecs * sampleRate - index;
            if (index < old_size - 1)
                resampled[i] = span[index] * (1 - frac) + span[index + 1] * frac;
            else
                resampled[i] = span[index];
        }

        return resampled;
    }

    public double[] Resample(Memory<double> samples, uint sampleRate, uint targetSampleRate)
    {
        var old_size = samples.Length;
        var span = samples.Span;
        var duration_secs = (double)old_size / sampleRate;
        var new_size = (int)(duration_secs * targetSampleRate);

        var resampled = new double[new_size];

        for (var i = 0; i < new_size; i++)
        {
            var timeSecs = (double)i / targetSampleRate;
            var index = (int)(timeSecs * sampleRate);

            var frac = timeSecs * sampleRate - index;
            if (index < old_size - 1)
                resampled[i] = span[index] * (1 - frac) + span[index + 1] * frac;
            else
                resampled[i] = span[index];
        }

        return resampled;
    }
}