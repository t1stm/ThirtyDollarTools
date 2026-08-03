namespace ThirtyDollarConverter.Encoder.PCM;

public enum AudioLayout
{
    /// <summary>
    ///     Uses only one channel of the <see cref="AudioData{T}" />. Assumes the channel is left only.
    /// </summary>
    AudioL,

    /// <summary>
    ///     Uses only one channel of the <see cref="AudioData{T}" />. Assumes the channel is right only.
    /// </summary>
    AudioR,

    /// <summary>
    ///     Uses only one channel of the <see cref="AudioData{T}" />. Outputs to both LR.
    /// </summary>
    AudioMono,

    /// <summary>
    ///     Uses two channels of the <see cref="AudioData{T}" />. Outputs to both LR.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    AudioStereoLR
}