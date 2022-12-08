namespace ThirtyDollarEncoder.PCM
{
    public class AudioData<T>
    {
        public AudioData(uint channelCount)
        {
            ChannelCount = channelCount;
            Samples = new T[ChannelCount][];
        }
        public readonly uint ChannelCount;
        public readonly T[][] Samples;

        public T[] GetChannel(int index) => Samples[index];
    }
}