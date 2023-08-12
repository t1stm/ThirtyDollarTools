using ThirtyDollarConverter.Resamplers;

namespace ThirtyDollarConverter;

public class EncoderSettings
{
    public int SampleRate;
    public int Channels;
    public readonly IResampler Resampler = new LinearResampler();
}