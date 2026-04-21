using ThirtyDollarEncoder.PCM;

namespace Shared.Audio;

public abstract class AudioContext
{
    public int SampleRate { get; protected set; } = 48000;
    public abstract string Name { get; }

    public abstract bool Create();
    public abstract void Destroy();
    public abstract bool CheckErrors();
    public abstract AudibleBuffer GetBufferObject(AudioData<float> sampleData, int sampleRate);
}