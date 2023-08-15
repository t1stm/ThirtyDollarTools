using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarEncoder.DPCM;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarEncoder.Wave;

namespace ThirtyDollarEncoder.Tests;

public class Tests
{
    public const string TestingFileLocation =
        "/home/kris/RiderProjects/ThirtyDollarWebsiteConverter/ThirtyDollarDebugApp/bin/Release/net7.0/Export/(Radiotomatosauce99) amogus of impostsussy [Ultrakill Act 2].🗿.wav";

    public AudioData<short> TestingData = null!;

    [SetUp]
    public void Setup()
    {
        using var fs = File.OpenRead(TestingFileLocation); 
        var decoder = new WaveDecoder();
        var data = decoder.Read(fs);

        TestingData = data.ReadAsInt16Array(false) ?? throw new NullReferenceException();
    }

    [Test]
    public void Test_DPCM_Functionality()
    {
        var left_channel = DPCMEncoder.Encode(TestingData.GetChannel(0));
        File.WriteAllBytes("./funny.dpcm", left_channel);
        
        var decoded = DPCMDecoder.DecodeToPcm(left_channel);
        var audio = decoded.ReadAsFloat32Array(false) ?? throw new NullReferenceException();
        
        var enc = new PcmEncoder(null!, null!, new EncoderSettings
        {
            SampleRate = 48000,
            Channels = 1
        });
        enc.WriteAsWavFile("./export.wav", audio);

        Assert.Pass();
    }
}