using ThirtyDollarConverter.Objects;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using Serilog;

namespace ThirtyDollarConverter.Tests;

public class PcmEncoderCrashTests
{
    [Fact]
    public void RenderEventToSlice_WithStartOffsetExceedingLength_DoesNotCrash()
    {
        // Arrange
        var settings = new EncoderSettings
        {
            SampleRate = 48000,
            Channels = 1
        };
        var logger = new LoggerConfiguration().CreateLogger();
        var holder = new SampleHolder(logger);
        var sound = new Sound { Id = "test" };
        var pcmData = new PcmDataHolder
        {
            SampleRate = 48000,
            Channels = 1,
            FloatData = new AudioData<float>(1)
        };
        pcmData.FloatData.Samples[0] = new float[1000];
        holder.SampleList.Add(sound, pcmData);
        holder.StringToSoundReferences.Add("test", sound);

        var encoder = new PcmEncoder(holder, settings);
        var mixer = new AudioMixer(AudioData<float>.WithLength(1, 2000), AudioLayout.AudioMono);
        
        var ev = new ExtendedEvent
        {
            SoundEvent = "test",
            Value = 0,
            OffsetInSeconds = 1.0 // 48000 samples at 48k, which is > 1000
        };
        var placement = new Placement
        {
            Event = ev,
            Index = 100,
            Audible = true
        };

        var processedEvents = new Dictionary<(string, double), ProcessedEvent>
        {
            { ("test", 0), new ProcessedEvent(ev) { AudioData = pcmData.FloatData } }
        };

        // Act & Assert
        var exception = Record.Exception(() => 
            encoder.GetType().GetMethod("RenderEventToSlice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(encoder, new object[] { 0, 2000, mixer, 0, placement, processedEvents, false })
        );
        
        Assert.Null(exception);
    }
}
