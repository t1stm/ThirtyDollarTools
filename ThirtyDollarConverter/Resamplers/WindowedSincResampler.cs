using System;

namespace ThirtyDollarConverter.Resamplers;

public class WindowedSincResampler : IResampler
{
    private static double Sinc(double x)
    {
        if (Math.Abs(x) < 1e-6)
            return 1.0;
        return Math.Sin(Math.PI * x) / (Math.PI * x);
    }

    private static double Hamming(double x, int N)
    {
        if (x < 0 || x >= N)
            return 0;
        return 0.54 - 0.46 * Math.Cos(2 * Math.PI * x / (N - 1));
    }

    private static double WindowedSinc(double x, int N)
    {
        return Sinc(x) * Hamming(x, N);
    }
    
    public float[] Resample(Span<float> samples, uint sample_rate, uint target_sample_rate)
    {
        throw new NotSupportedException("This resampler doesn't support single precision data.");
    }

    public double[] Resample(Span<double> samples, uint sample_rate, uint target_sample_rate)
    {
        var factor = target_sample_rate / sample_rate;
        var outputLength = samples.Length * factor;
        var output = new double[outputLength];

        for (var i = 0; i < outputLength; i++)
        {
            var t = i / (double)factor;
            double sum = 0;
            for (var j = 0; j < samples.Length; j++)
            {
                sum += samples[j] * WindowedSinc(t - j, samples.Length);
            }
            output[i] = sum;
        }

        return output;
    }
}