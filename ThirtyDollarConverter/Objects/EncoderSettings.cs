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
    /// Due to the nature of the TDW before the Thirty Dollar Rewrite got merged, some sequences sound wrong when played perfectly.
    /// This controls how much time after the combine event, a sound is played.
    /// </summary>
    public uint CombineDelayMs = 0;
    
    /// <summary>
    /// Select the resampler you want to use.
    /// </summary>
    public IResampler Resampler = new LinearResampler();

    /// <summary>
    /// This adds calculated timings to the placement.
    /// </summary>
    public bool AddVisualEvents { get; set; }
}