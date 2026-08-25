using ThirtyDollarConverter.Encoder.Resamplers;

namespace VisualizerScene.Settings;

/// <summary>
///     The resamplers the settings screen offers, keyed by the name written to the settings
///     file. Stored as a name plus loose numbers rather than the object itself because the
///     settings file only round-trips primitives - <see cref="Create" /> is what turns them
///     back into a resampler, and everything that plays audio asks for one from here rather
///     than newing one up.
/// </summary>
public static class Resamplers
{
    public const string Hermite = "Hermite";
    public const string Linear = "Linear";
    public const string None = "No interpolation";
    public const string SincHann = "Sinc (Hann)";
    public const string SincKaiserBest = "Sinc (Kaiser best)";
    public const string SincKaiserFast = "Sinc (Kaiser fast)";
    public const string ByteCruncher = "Byte cruncher";

    /// <summary>In the order the settings screen lists them: cheapest first.</summary>
    public static readonly string[] Names =
        [Hermite, Linear, None, SincHann, SincKaiserBest, SincKaiserFast, ByteCruncher];

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
            SincKaiserBest => new KaiserBestResampler(),
            SincKaiserFast => new KaiserFastResampler(),
            ByteCruncher => new ByteCruncherResampler(settings.CruncherBits),
            _ => new HermiteResampler()
        };
    }
}
