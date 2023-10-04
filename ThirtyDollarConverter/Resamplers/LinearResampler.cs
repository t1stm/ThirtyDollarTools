using System;

namespace ThirtyDollarConverter.Resamplers;

public class LinearResampler : IResampler
{
    public float[] Resample(Span<float> samples, uint sampleRate, uint targetSampleRate)
    {
        var oldSize = samples.Length;
        var durationSecs = (float)oldSize / sampleRate;
        var newSize = (int)(durationSecs * targetSampleRate);

        var resampled = new float[newSize];

        for (var i = 0; i < newSize; i++)
        {
            var timeSecs = (float) i / targetSampleRate;
            var index = (int) (timeSecs * sampleRate);

            var frac = timeSecs * sampleRate - index;
            if (index < oldSize - 1)
                resampled[i] = samples[index] * (1 - frac) + samples[index + 1] * frac;
            else
                resampled[i] = samples[index];
        }

        return resampled;
    }
    
    public double[] Resample(Span<double> samples, uint sampleRate, uint targetSampleRate)
    {
        var oldSize = samples.Length;
        var durationSecs = (float)oldSize / sampleRate;
        var newSize = (int)(durationSecs * targetSampleRate);

        var resampled = new double[newSize];

        for (var i = 0; i < newSize; i++)
        {
            var timeSecs = (float) i / targetSampleRate;
            var index = (int) (timeSecs * sampleRate);

            var frac = timeSecs * sampleRate - index;
            if (index < oldSize - 1)
                resampled[i] = samples[index] * (1 - frac) + samples[index + 1] * frac;
            else
                resampled[i] = samples[index];
        }

        return resampled;
    }
}