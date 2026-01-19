using System.Numerics;

namespace ThirtyDollarEncoder.PCM;

public class AudioData<T> : IDisposable
    where T : INumber<T>,
    IComparable<T>,
    IEquatable<T>,
    IMultiplyOperators<T, T, T>,
    IDivisionOperators<T, T, T>
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

    public static AudioData<float> Empty(uint channelCount)
    {
        var empty = Array.Empty<float>();
        var data = new AudioData<float>(channelCount);
        for (var i = 0; i < channelCount; i++) data.Samples[i] = empty;

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

    public void Normalize(float maxVolume = 1f)
    {
        lock (Samples)
        {
            if (Samples.Length < 1 || Samples[0].Length < 1) return;
            var maxSampleVolume = Samples[0][0];

            foreach (var channel in Samples)
            foreach (var sample in channel)
                if (maxSampleVolume < sample)
                    maxSampleVolume = sample;

            foreach (var channel in Samples)
                for (var index = 0; index < channel.Length; index++)
                    channel[index] /= maxSampleVolume ?? throw new Exception("Max sample volume is null.");
        }
    }

    public int GetLength()
    {
        return GetChannel(0).Length;
    }
}