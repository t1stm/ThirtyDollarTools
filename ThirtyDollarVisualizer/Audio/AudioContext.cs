using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio;

public abstract class AudioContext
{
    public int SampleRate { get; protected set; } = 48000;
    public float GlobalVolume { get; set; } = .25f;

    public abstract bool Create();
    public abstract void Destroy();
    public abstract bool CheckErrors();
    public abstract AudibleBuffer GetBufferObject(AudioData<float> sample_data, int sample_rate);
}