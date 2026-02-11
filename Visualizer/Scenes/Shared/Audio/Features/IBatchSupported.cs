namespace Shared.Audio.Features;

public interface IBatchSupported
{
    public void PlayBatch(Span<AudibleBuffer> buffers);
}