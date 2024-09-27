namespace ThirtyDollarEncoder.PCM;

public class AudioData<T> : IDisposable
{
    public readonly uint ChannelCount;
    public T[][] Samples;

    public AudioData(uint channelCount)
    {
        ChannelCount = channelCount;
        Samples = new T[ChannelCount][];
    }

    public void Dispose()
    {
        Samples = [];
        GC.SuppressFinalize(this);
    }

    public static AudioData<float> Empty(uint channel_count)
    {
        var empty = Array.Empty<float>();
        var data = new AudioData<float>(channel_count);
        for (var i = 0; i < channel_count; i++) data.Samples[i] = empty;

        return data;
    }

    public static AudioData<float> WithLength(uint channels, int length)
    {
        var data = new AudioData<float>(channels);
        for (var i = 0; i < channels; i++) data.Samples[i] = new float[length];
        return data;
    }

    public T[] GetChannel(int index)
    {
        lock (Samples)
        {
            return Samples[index];
        }
    }

    public int GetLength()
    {
        return GetChannel(0).Length;
    }
}