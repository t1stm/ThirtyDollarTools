using ThirtyDollarConverter.Resamplers;

namespace ThirtyDollarConverter.Objects;

public class EncoderSettings
{
    /// <summary>
    /// The sample rate to export in.
    /// </summary>
    public uint SampleRate;
    
    /// <summary>
    /// The amount of audio channels. (1 - 2 supported at the moment) 
    /// </summary>
    public uint Channels;
    
    /// <summary>
    /// How long the cut event lowers the event's volume before absolutely stopping it. Value is in milliseconds.
    /// </summary>
    public uint CutDelayMs = 25;
    
    /// <summary>
    /// Select the resampler you want to use.
    /// </summary>
    public readonly IResampler Resampler = new LinearResampler();
}