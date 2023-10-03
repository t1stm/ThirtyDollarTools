namespace ThirtyDollarConverter.Resamplers;

public class LinearResampler : IResampler
{
    public float[] Resample(float[] samples, uint sampleRate, uint targetSampleRate)
    {
        var oldSize = (ulong)samples.LongLength;
        var durationSecs = (float)oldSize / sampleRate;
        var newSize = (ulong)(durationSecs * targetSampleRate);

        var resampled = new float[newSize];

        for (ulong i = 0; i < newSize; i++)
        {
            var timeSecs = (float) i / targetSampleRate;
            var index = (ulong) (timeSecs * sampleRate);

            var frac = timeSecs * sampleRate - index;
            if (index < oldSize - 1)
                resampled[i] = samples[index] * (1 - frac) + samples[index + 1] * frac;
            else
                resampled[i] = samples[index];
        }

        return resampled;
    }
}