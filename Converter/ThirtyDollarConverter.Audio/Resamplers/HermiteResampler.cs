namespace ThirtyDollarEncoder.Resamplers;

public class HermiteResampler : IResampler
{
    public float[] Resample(Memory<float> samples, uint sampleRate, uint targetSampleRate)
    {
        var span = samples.Span;
        if (sampleRate == targetSampleRate)
            // No resampling needed
            return span.ToArray();

        var new_length = (int)Math.Ceiling(span.Length * (double)targetSampleRate / sampleRate);
        var resampled = new float[new_length];

        var factor = targetSampleRate / (double)sampleRate;

        for (var i = 0; i < new_length; i++)
        {
            var original_index = i / factor;
            var left = (int)original_index;

            resampled[i] = HermiteInterpolation(samples, left, original_index - left);
        }

        return resampled;
    }

    public double[] Resample(Memory<double> samples, uint sampleRate, uint targetSampleRate)
    {
        var span = samples.Span;
        if (sampleRate == targetSampleRate)
            // No resampling needed
            return span.ToArray();

        var new_length = (int)Math.Ceiling(span.Length * (double)targetSampleRate / sampleRate);
        var resampled = new double[new_length];

        var factor = targetSampleRate / (double)sampleRate;

        for (var i = 0; i < new_length; i++)
        {
            var original_index = i / factor;
            var left = (int)original_index;

            resampled[i] = HermiteInterpolation(samples, left, original_index - left);
        }

        return resampled;
    }

    private static float HermiteInterpolation(Memory<float> samples, int startIndex, double fraction)
    {
        var length = samples.Length;
        var span = samples.Span;

        Span<double> p = stackalloc double[4];

        for (var i = 0; i < 4; i++)
        {
            var index = startIndex - 1 + i;

            p[i] = index switch
            {
                >= 0 when index < length => span[index],
                < 0 => span[0],
                _ => span[length - 1]
            };
        }

        var c0 = p[1];
        var c1 = 0.5 * (p[2] - p[0]);
        var c2 = p[0] - 2.5 * p[1] + 2 * p[2] - 0.5 * p[3];
        var c3 = 0.5 * (p[3] - p[0]) + 1.5 * (p[1] - p[2]);

        return (float)(c3 * fraction * fraction * fraction + c2 * fraction * fraction + c1 * fraction + c0);
    }

    private static double HermiteInterpolation(Memory<double> samples, int startIndex, double fraction)
    {
        var length = samples.Length;
        var span = samples.Span;

        Span<double> p = stackalloc double[4];

        for (var i = 0; i < 4; i++)
        {
            var index = startIndex - 1 + i;

            p[i] = index switch
            {
                >= 0 when index < length => span[index],
                < 0 => span[0],
                _ => span[length - 1]
            };
        }

        var c0 = p[1];
        var c1 = 0.5 * (p[2] - p[0]);
        var c2 = p[0] - 2.5 * p[1] + 2 * p[2] - 0.5 * p[3];
        var c3 = 0.5 * (p[3] - p[0]) + 1.5 * (p[1] - p[2]);

        return c3 * fraction * fraction * fraction + c2 * fraction * fraction + c1 * fraction + c0;
    }
}