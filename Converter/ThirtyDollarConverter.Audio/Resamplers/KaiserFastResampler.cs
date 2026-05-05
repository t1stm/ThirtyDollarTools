namespace ThirtyDollarEncoder.Resamplers;

public sealed class KaiserFastResampler() : KaiserSincResampler(
    beta: 9.90322,
    rolloff: 0.8682120388377784,
    numZeros: 24,
    precision: 512);
