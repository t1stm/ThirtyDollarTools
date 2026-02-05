using ThirtyDollarEncoder.PCM;
using ThirtyDollarVisualizer.Audio.Features;

namespace ThirtyDollarVisualizer.Audio.Null;

public class NullAudioContext : AudioContext, IBatchSupported
{
    public override string Name => "Null";

    public void PlayBatch(Span<AudibleBuffer> buffers)
    {
    }

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

    public override AudibleBuffer GetBufferObject(AudioData<float> sampleData, int sampleRate)
    {
        return new NullAudibleBuffer(sampleData, sampleRate);
    }
}