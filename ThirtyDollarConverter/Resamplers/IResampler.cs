namespace ThirtyDollarConverter.Resamplers;

public interface IResampler
{
    float[] Resample(float[] samples, uint sampleRate, uint targetSampleRate);
}