using System;
using System.Threading.Tasks;
using ThirtyDollarConverter.Encoder.PCM;

namespace ThirtyDollarConverter.Voices;

/// <summary>Offline mixing of a whole <see cref="VoiceList" />, for exports and previews.</summary>
public static class VoiceMixer
{
    /// <summary>
    ///     Samples per parallel slice. Smaller slices don't help: the mix is memory-bound past a
    ///     few threads, and each slice pays a binary search.
    /// </summary>
    public const int SliceLength = 4096;

    /// <summary>
    ///     Mixes the whole list into one buffer, slices in parallel. The result doesn't depend on
    ///     the slice length or thread count.
    /// </summary>
    public static AudioData<float> MixAll(VoiceList list, int sliceLength = SliceLength)
    {
        var audio = AudioData<float>.WithLength((uint)list.Channels, list.Length);
        var left = audio.Samples[0];
        var right = list.Channels > 1 ? audio.Samples[1] : [];
        var slices = (list.Length + sliceLength - 1) / sliceLength;

        Parallel.For(0, slices, slice =>
        {
            var from = slice * sliceLength;
            var to = Math.Min(from + sliceLength, list.Length);
            var starts = list.Starts;
            var voices = list.Voices;

            for (var i = list.LowerBound((long)from - list.Longest); i < starts.Length && starts[i] < to; i++)
            {
                ref readonly var voice = ref voices[i];
                if (voice.End <= from) continue;

                list.MixRange(i, Math.Max(from, voice.Start), Math.Min(to, voice.End), from,
                    left.AsSpan(from, to - from), right.Length > 0 ? right.AsSpan(from, to - from) : default);
            }
        });

        return audio;
    }
}
