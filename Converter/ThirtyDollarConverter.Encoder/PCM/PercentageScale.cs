namespace ThirtyDollarConverter.Encoder.PCM;

/// <summary>
///     Defines the scaling methods available for percentage-based operations.
/// </summary>
public enum PercentageScale
{
    Linear,
    LinearOverflowLogarithmic,
    Logarithmic,

    /// <summary>
    ///     Equal-power (constant-power) scaling, matching the Web Audio API's
    ///     StereoPannerNode curve used by the Thirty Dollar Website. Only meaningful for
    ///     panning.
    /// </summary>
    EqualPower
}
