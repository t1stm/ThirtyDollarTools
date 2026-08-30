using ThirtyDollarConverter.Encoder.Resamplers;

namespace VisualizerScene.Settings;

/// <summary>
///     The resamplers the settings screen offers, keyed by the name written to the settings
///     file. Settings hold a name plus loose numbers, since the file only round-trips
///     primitives; <see cref="Create" /> turns them back into a resampler, and every caller
///     that plays audio goes through it rather than constructing one.
/// </summary>
public static class Resamplers
{
    public const string Hermite = "Hermite";
    public const string Linear = "Linear";
    public const string None = "No interpolation";
    public const string SincHann = "Sinc (Hann)";
    public const string ByteCruncher = "Byte cruncher";

    /// <summary>In the order the settings screen lists them: cheapest first.</summary>
    public static readonly string[] Names =
        [Hermite, Linear, None, SincHann, ByteCruncher];

    /// <summary>The settings whose value changes what <see cref="Create" /> returns.</summary>
    public static readonly string[] Properties =
    [
        nameof(VisualizerSettings.Resampler), nameof(VisualizerSettings.SincFilterSize),
        nameof(VisualizerSettings.SincPrecision), nameof(VisualizerSettings.CruncherBits)
    ];

    /// <summary>An unknown name falls back to the default rather than throwing: the settings file is hand-editable.</summary>
    public static IResampler Create(VisualizerSettings settings)
    {
        return settings.Resampler switch
        {
            Linear => new LinearResampler(),
            None => new NoInterpolationResampler(),
            SincHann => new HannSincResampler(settings.SincFilterSize, settings.SincPrecision),
            ByteCruncher => new ByteCruncherResampler(settings.CruncherBits),
            _ => new HermiteResampler()
        };
    }
}
