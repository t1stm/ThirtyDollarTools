namespace ThirtyDollarConverter.Encoder.Resamplers;

public sealed class KaiserFastResampler() : KaiserSincResampler(
    9.90322,
    0.8682120388377784,
    24,
    512);