using ThirtyDollarConverter.Audio.Resamplers;
using ThirtyDollarConverter.Objects;

namespace ThirtyDollarConverter.DiscordBot;

public class Static
{
    public static SampleHolder SampleHolder { get; set; } = new();
    public static EncoderSettings EncoderSettings { get; set; } = new()
    {
        Channels = 2,
        CutFadeLengthMs = 10,
        EnableNormalization = true,
        Resampler = new HermiteResampler(),
        SampleRate = 48000
    };
}