namespace ThirtyDollarVisualizer.Audio.FeatureFlags;

public interface IBatchSupported
{
    public void PlayBatch(Span<AudibleBuffer> buffers);
}