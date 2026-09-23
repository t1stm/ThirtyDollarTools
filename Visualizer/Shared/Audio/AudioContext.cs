using ThirtyDollarConverter.Encoder.PCM;

namespace Shared.Audio;

public abstract class AudioContext
{
    public int SampleRate { get; protected set; } = 48000;
    public abstract string Name { get; }

    public abstract bool Create();
    public abstract void Destroy();
    public abstract bool CheckErrors();
    public abstract AudibleBuffer GetBufferObject(AudioData<float> sampleData, int sampleRate);

    /// <summary>
    ///     A buffer holding the samples the way the source stores them - 8-bit, 16-bit or 32-bit
    ///     float, in the source's own channel count - rather than widened to stereo float, which
    ///     doubles a 16-bit stereo file and quadruples a 16-bit mono one. 24-bit, which neither
    ///     library can hold, is widened to float: the smallest format that keeps it whole.
    /// </summary>
    public abstract AudibleBuffer GetBufferObject(PcmDataHolder pcm);
}