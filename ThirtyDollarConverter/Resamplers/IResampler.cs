using System;

namespace ThirtyDollarConverter.Resamplers;

public interface IResampler
{
    /// <summary>
    /// Method that resamples given audio data to another sample rate.
    /// </summary>
    /// <param name="samples">The original sample data.</param>
    /// <param name="sampleRate">The original sample rate.</param>
    /// <param name="targetSampleRate">The target sample rate.</param>
    /// <returns></returns>
    float[] Resample(Span<float> samples, uint sampleRate, uint targetSampleRate);
    double[] Resample(Span<double> samples, uint sampleRate, uint targetSampleRate);
}