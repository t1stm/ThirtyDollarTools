namespace ThirtyDollarEncoder.Resamplers;

public class BandlimitedResampler(int filterSize = 64) : IResampler
{
    /// <summary>
    /// Resamples the given audio data to another sample rate using bandlimited interpolation.
    /// </summary>
    /// <param name="samples">The original sample data.</param>
    /// <param name="sampleRate">The original sample rate.</param>
    /// <param name="targetSampleRate">The target sample rate.</param>
    /// <returns>Resampled audio data.</returns>
    public float[] Resample(Memory<float> samples, uint sampleRate, uint targetSampleRate)
    {
        var resample_ratio = (double)targetSampleRate / sampleRate;
        var samples_length = (int)(samples.Length * resample_ratio);
        var output = new float[samples_length];

        for (var i = 0; i < samples_length; i++)
        {
            var sample_position = i / resample_ratio;
            var sample_index = (int)Math.Floor(sample_position);

            var result = 0.0f;

            for (var j = sample_index - filterSize; j <= sample_index + filterSize; j++)
            {
                if (j < 0 || j >= samples.Length) continue;

                var x = sample_position - j;
                var window = samples.Span[j] * Sinc(x) * HannWindow(x / filterSize);
                result += (float)window;
            }

            output[i] = result;
        }

        return output;
    }

    public double[] Resample(Memory<double> samples, uint sampleRate, uint targetSampleRate)
    {
        var resample_ratio = (double)targetSampleRate / sampleRate;

        var samples_length = (int)(samples.Length * resample_ratio);
        var output = new double[samples_length];

        for (var i = 0; i < samples_length; i++)
        {
            var sample_position = i / resample_ratio;
            var sample_index = (int)Math.Floor(sample_position);

            var result = 0.0d;

            for (var j = sample_index - filterSize; j <= sample_index + filterSize; j++)
            {
                if (j < 0 || j >= samples.Length) continue;

                var x = sample_position - j;
                var window = samples.Span[j] * Sinc(x) * HannWindow(x / filterSize);
                result += window;
            }

            output[i] = result;
        }

        return output;
    }

    /// <summary>
    /// Sinc function for bandlimited interpolation.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>Sinc function output.</returns>
    private static double Sinc(double x)
    {
        if (x == 0.0)
            return 1.0;

        x *= Math.PI;
        return Math.Sin(x) / x;
    }

    /// <summary>
    /// Hann window function to reduce artifacts in the sinc interpolation.
    /// </summary>
    /// <param name="x">The normalized input value, scaled by the filter radius.</param>
    /// <returns>Hann window output.</returns>
    private static double HannWindow(double x)
    {
        return 0.5 * (1.0 + Math.Cos(2.0 * Math.PI * x));
    }
}