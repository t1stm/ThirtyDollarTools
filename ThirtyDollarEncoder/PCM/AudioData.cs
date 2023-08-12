namespace ThirtyDollarEncoder.PCM;

public class AudioData<T>
{
    public readonly uint ChannelCount;
    public readonly T[][] Samples;

    public AudioData(uint channelCount)
    {
        ChannelCount = channelCount;
        Samples = new T[ChannelCount][];
    }

    public static AudioData<float> Empty(uint channelCount)
    {
        var empty = Array.Empty<float>();
        var data = new AudioData<float>(channelCount);
        for (var i = 0; i < channelCount; i++) data.Samples[i] = empty;

        return data;
    }

    public T[] GetChannel(int index)
    {
        return Samples[index];
    }
}