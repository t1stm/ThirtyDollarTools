namespace ThirtyDollarConverter.Encoder.Resamplers;

public sealed class KaiserBestResampler() : KaiserSincResampler(
    12.9846,
    0.9173473712608761,
    50,
    8192);