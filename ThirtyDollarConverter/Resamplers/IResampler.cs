using System;

namespace ThirtyDollarConverter.Resamplers;

public interface IResampler
{
    /// <summary>
    /// Method that resamples given audio data to another sample rate.
    /// </summary>
    /// <param name="samples">The original sample data.</param>
    /// <param name="sample_rate">The original sample rate.</param>
    /// <param name="target_sample_rate">The target sample rate.</param>
    /// <returns></returns>
    float[] Resample(Memory<float> samples, uint sample_rate, uint target_sample_rate);
    double[] Resample(Memory<double> samples, uint sample_rate, uint target_sample_rate);
}