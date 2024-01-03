using ThirtyDollarEncoder.PCM;
using ThirtyDollarVisualizer.Audio.FeatureFlags;

namespace ThirtyDollarVisualizer.Audio.Null;

public class NullAudioContext : AudioContext, IBatchSupported
{
    public override bool Create()
    {
        return false;
    }

    public override void Destroy()
    {
    }

    public override bool CheckErrors()
    {
        return false;
    }

    public override AudibleBuffer GetBufferObject(AudioData<float> sample_data, int sample_rate)
    {
        return new NullAudibleBuffer(sample_data, sample_rate);
    }

    public void PlayBatch(Span<AudibleBuffer> buffers)
    {
    }
}