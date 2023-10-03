using System;

namespace ThirtyDollarConverter.Resamplers;

[Obsolete("Please do not use this resampler, because it currently doesn't work.")]
public class PreciseSincResampler : IResampler
{
    public float[] Resample(float[] samples, uint sampleRate, uint targetSampleRate)
    {
        var resampled_samples = new float[samples.Length * targetSampleRate / sampleRate];

        for (var i = 0; i < resampled_samples.Length; i++)
        {
            float sum = 0;
            var time = (float) i * sampleRate / targetSampleRate;

            for (var j = 0; j < samples.Length; j++)
            {
                var sinc = (float) (Math.PI * (time - j) / sampleRate);

                if (Math.Abs(sinc) < 1e-10)
                {
                    sum += samples[j];
                }
                else
                {
                    sum += (float) (samples[j] * Math.Sin(sinc) / sinc);
                }
            }

            resampled_samples[i] = sum;
        }

        return resampled_samples;
    }
}