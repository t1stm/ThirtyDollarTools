using ThirtyDollarConverter.Encoder.PCM;

namespace Shared.Audio.Null;

public class NullAudioContext : AudioContext
{
    public override string Name => "Null";

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
        return new NullAudibleBuffer();
    }
}