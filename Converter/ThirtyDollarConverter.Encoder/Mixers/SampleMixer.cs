using System.Numerics;
using ThirtyDollarConverter.Encoder.PCM;

namespace ThirtyDollarConverter.Encoder.Mixers;

/// <summary>
///     Buffer-level primitives for rendering one sound's samples into a mix, shared by the
///     encoder's per-event render path.
/// </summary>
public static class SampleMixer
{
    /// <summary>
    ///     Adds a source audio data array to a destination.
    /// </summary>
    /// <param name="source">The source audio data you want to add.</param>
    /// <param name="destination">The destination you want to add to.</param>
    /// <param name="index">The index of the destination you want to start on.</param>
    /// <param name="volume">The volume of the source audio while being added.</param>
    /// <param name="volumeScale"></param>
    /// <param name="length">The length of the export you want to do.</param>
    /// <param name="offset">The source sample offset. Used in multithreading.</param>
    public static void RenderSample(Span<float> source, Span<float> destination, int index,
        double volume, PercentageScale volumeScale, int length = -1, int offset = -1)
    {
        if (length == -1) length = source.Length;

        if (offset < 0) offset = 0;

        var s_slice = source.Slice(offset, length);
        var d_slice = destination[index..];
        var chunk_size = Vector<float>.Count;
        var final_volume = (float)volume / 100f;
        switch (volumeScale)
        {
            case PercentageScale.Logarithmic:
            case PercentageScale.LinearOverflowLogarithmic when final_volume > 1f:
                final_volume = MathF.Sqrt(final_volume);
                break;
        }

        var s_chunked = s_slice.Length - s_slice.Length % chunk_size;
        var d_chunked = d_slice.Length - d_slice.Length % chunk_size;

        var min = Math.Min(s_chunked, d_chunked);

        for (var i = 0; i < min; i += chunk_size)
        {
            var i_chunk = i + chunk_size;

            var s = s_slice[i..i_chunk];
            var d = d_slice[i..i_chunk];

            var d_vector = new Vector<float>(d);
            var s_vector = new Vector<float>(s);

            var final = d_vector + s_vector * final_volume;

            final.CopyTo(d);
        }

        var min_final = Math.Min(d_slice.Length, s_slice.Length);
        for (var i = min; i < min_final; i++) d_slice[i] += s_slice[i] * final_volume;
    }

    /// <summary>
    ///     Zeroes out <paramref name="mixSlice" /> from <paramref name="currentStart" /> onward for a
    ///     <c>!cut</c>/<c>#icut</c> event: fades over <paramref name="cutFadeLengthMs" />, then holds
    ///     silence until it finds a run of already-silent samples to stop at.
    /// </summary>
    public static void HandleCut(int start, int end, int currentStart, Span<float> mixSlice, uint sampleRate,
        uint cutFadeLengthMs)
    {
        var wanted_zero_samples = 4096 * sampleRate / 48000;
        var norm_start = currentStart - start;
        var norm_end = end - start;

        var zero_samples = 0;
        var zero_index = norm_end;
        for (var i = norm_start; i < norm_end; i++)
        {
            if (zero_samples >= wanted_zero_samples)
            {
                zero_index = i;
                break;
            }

            zero_samples++;

            if (i >= 0 && mixSlice[i] == 0f) continue;
            zero_samples = 0;
        }

        var cut_fade_ms = (int)cutFadeLengthMs;
        var cut_fade_length = (int)(sampleRate / 1000) * cut_fade_ms;
        var cut_fade_end = norm_start + cut_fade_length;

        int cut_i;
        for (cut_i = norm_start; cut_i < cut_fade_end; cut_i++)
        {
            if (cut_i < 0 || cut_i >= zero_index) continue;
            var norm_i = cut_fade_end - cut_i;

            var delta = (float)norm_i / cut_fade_length;
            mixSlice[cut_i] *= delta;
        }

        for (var i = cut_i; i < zero_index; i++)
        {
            if (i < 0) continue;
            mixSlice[i] = 0f;
        }
    }
}