using ThirtyDollarConverter.Encoder.PCM;

namespace ThirtyDollarConverter.Encoder.Mixers;

public interface IMixingMethod
{
    public AudioData<float> MixTracks((AudioLayout, AudioData<float>)[] tracks);
}