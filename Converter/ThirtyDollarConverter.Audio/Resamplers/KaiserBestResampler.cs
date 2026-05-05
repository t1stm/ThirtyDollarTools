namespace ThirtyDollarEncoder.Resamplers;

public sealed class KaiserBestResampler() : KaiserSincResampler(
    beta: 12.9846,
    rolloff: 0.9173473712608761,
    numZeros: 50,
    precision: 8192);
