using System;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Encoder.Resamplers;

namespace ThirtyDollarConverter.Objects;

public class EncoderSettings
{
    /// <summary>
    ///     This adds calculated timings to the placement.
    /// </summary>
    public bool AddVisualEvents;

    /// <summary>
    ///     The amount of audio channels. (1 - 2 supported at the moment)
    /// </summary>
    public uint Channels;

    /// <summary>
    ///     How long after a "!combine" event a sound is played, in milliseconds. Compensates for
    ///     pre-rewrite TDW sequences that sound wrong when played with perfect timing.
    /// </summary>
    public uint CombineDelayMs = 0;

    /// <summary>
    ///     How long the cut event lowers the event's volume before absolutely stopping it. Value is in milliseconds.
    /// </summary>
    public uint CutFadeLengthMs = 4;

    /// <summary>
    ///     This controls whether the converter should normalize the final export of a cover.
    /// </summary>
    public bool EnableNormalization = true;

    /// <summary>
    ///     How many slices to separate the sequence in for multithreading.
    /// </summary>
    public int MultithreadingSlices = Environment.ProcessorCount * 4;

    /// <summary>
    ///     When enabled, clamps BPM to [5, 20000] after every "!speed" event, matching TDW.
    ///     Off by default.
    /// </summary>
    public bool ClampBpm;

    /// <summary>
    ///     When enabled, clamps the global volume to [0, 600] after every "!volume" event,
    ///     matching TDW. Off by default (the volume is still floored at 0 regardless).
    /// </summary>
    public bool ClampVolume;

    /// <summary>
    ///     When enabled, clamps the running transpose value to [-60, 60] after every
    ///     "!transpose" event, matching TDW. Off by default.
    /// </summary>
    public bool ClampTranspose;

    /// <summary>
    ///     When enabled, clamps each note's final pitch (its own value plus the running
    ///     transpose) to [-72, 72], matching TDW. Off by default.
    /// </summary>
    public bool ClampPitch;

    /// <summary>
    ///     When enabled, clamps each note's own volume ratio to [0, 4] (0-400%) before it's
    ///     multiplied by the global volume, matching TDW. Off by default.
    /// </summary>
    public bool ClampNoteVolume;

    /// <summary>
    ///     Represents the scaling method applied to adjust pan values during audio rendering.
    ///     Determines how the percentage-based pan adjustments are calculated,
    ///     affecting the balance between the left and right audio channels.
    /// </summary>
    public PercentageScale PanScale = PercentageScale.EqualPower;

    /// <summary>
    ///     Select the resampler you want to use.
    /// </summary>
    public IResampler Resampler = new HannSincResampler();

    /// <summary>
    ///     The sample rate to export in.
    /// </summary>
    public uint SampleRate;

    /// <summary>
    ///     Specifies the scaling method used to interpret volume levels.
    /// </summary>
    public PercentageScale VolumeScale = PercentageScale.LinearOverflowLogarithmic;

    public string DownloadLocation { get; set; } = string.Empty;
}