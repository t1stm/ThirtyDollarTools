namespace ThirtyDollarEncoder.PCM;

public class PcmDataHolder
{
    public readonly SemaphoreSlim Semaphore = new(1);
    public AudioData<float>? FloatData = null;
    public AudioData<short>? ShortData = null;
    public uint SampleRate { get; set; }
    public uint Channels { get; set; }
    public uint Samples { get; set; }
    public Encoding Encoding { get; set; }
    public byte[]? AudioData { get; set; }
    public AdditionalData? AdditionalData { get; set; } = null;

    public override string ToString()
    {
        return
            $"PcmDataHolder: SampleRate={SampleRate}, Channels={Channels}, Samples={Samples}, Encoding={Encoding}, AdditionalData={AdditionalData}";
    }
}