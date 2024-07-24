using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarEncoder.Mixers;

public interface IMixingMethod
{
    public AudioData<float> MixTracks((AudioLayout, AudioData<float>)[] tracks);
}