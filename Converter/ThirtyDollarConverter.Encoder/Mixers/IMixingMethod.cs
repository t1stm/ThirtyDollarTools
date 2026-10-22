using ThirtyDollarConverter.Encoder.PCM;

namespace ThirtyDollarConverter.Encoder.Mixers;

[Obsolete("Only the legacy track renderer mixes through per-track buffers.")]
public interface IMixingMethod
{
    public AudioData<float> MixTracks((AudioLayout, AudioData<float>)[] tracks);
}