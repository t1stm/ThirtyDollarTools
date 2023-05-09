namespace ThirtyDollarEncoder.PCM;

public class PcmDataHolder
{
    public readonly object LockObject = new();
    public AudioData<float>? FloatData = null;
    public AudioData<short>? ShortData = null;
    public uint SampleRate { get; set; }
    public uint Channels { get; set; }
    public Encoding Encoding { get; set; }
    public byte[]? AudioData { get; set; }
    public AdditionalData? AdditionalData { get; set; } = null;
}

public enum Encoding
{
    Int8 = 8,
    Int16 = 16,
    Int24 = 24,
    Float32 = 32
}