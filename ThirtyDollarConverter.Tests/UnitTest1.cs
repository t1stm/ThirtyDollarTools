using ThirtyDollarEncoder.PCM;
using ThirtyDollarEncoder.Wave;

namespace ThirtyDollarEncoder.Tests;

public class Tests
{
    public const string TestingFileLocation =
        "/home/kris/RiderProjects/ThirtyDollarWebsiteConverter/ThirtyDollarConverter.Debug/bin/Release/net7.0/Export/(Radiotomatosauce99) amogus of impostsussy [Ultrakill Act 2].🗿.wav";

    public AudioData<short> TestingData = null!;

    [SetUp]
    public void Setup()
    {
        using var fs = File.OpenRead(TestingFileLocation);
        var decoder = new WaveDecoder();
        var data = decoder.Read(fs);

        TestingData = data.ReadAsInt16Array(false) ?? throw new NullReferenceException();
    }
}