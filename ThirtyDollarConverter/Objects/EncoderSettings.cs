using ThirtyDollarConverter.Resamplers;

namespace ThirtyDollarConverter.Objects;

public class EncoderSettings
{
    public uint SampleRate;
    public uint Channels;
    public readonly IResampler Resampler = new LinearResampler();
}