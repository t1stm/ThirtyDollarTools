using Serilog;
using ThirtyDollarConverter.Encoder.Resamplers;
using ThirtyDollarConverter.Objects;

namespace ThirtyDollarConverter.DiscordBot;

public static class Static
{
    public static ILogger Logger { get; set; } = null!;
    public static SampleHolder SampleHolder { get; set; } = null!;

    public static EncoderSettings EncoderSettings { get; set; } = new()
    {
        Channels = 2,
        CutFadeLengthMs = 10,
        EnableNormalization = true,
        Resampler = new HermiteResampler(),
        SampleRate = 48000
    };
}