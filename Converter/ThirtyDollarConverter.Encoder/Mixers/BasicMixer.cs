using System.Numerics;
using ThirtyDollarConverter.Encoder.PCM;

namespace ThirtyDollarConverter.Encoder.Mixers;

public class BasicMixer : IMixingMethod
{
    public AudioData<float> MixTracks((AudioLayout, AudioData<float>)[] tracks)
    {
        var length = tracks[0].Item2.GetLength();
        var export_track = AudioData<float>.WithLength(2, length);

        foreach (var (layout, audio_data) in tracks)
            switch (layout)
            {
                case AudioLayout.AudioL:
                {
                    var left = audio_data.GetChannel(0);
                    var left_export = export_track.GetChannel(0);

                    BasicMix(left, left_export);
                    break;
                }

                case AudioLayout.AudioR:
                {
                    var right = audio_data.GetChannel(0);
                    var right_export = export_track.GetChannel(0);

                    BasicMix(right, right_export);
                    break;
                }

                case AudioLayout.AudioStereoLR:
                {
                    var l = audio_data.GetChannel(0);
                    var l_export = export_track.GetChannel(0);

                    var r = audio_data.GetChannel(1);
                    var r_export = export_track.GetChannel(1);

                    BasicMix(l, l_export);
                    BasicMix(r, r_export);
                    break;
                }

                case AudioLayout.AudioMono:
                {
                    var mono = audio_data.GetChannel(0);
                    var l_export = export_track.GetChannel(0);
                    var r_export = export_track.GetChannel(1);

                    BasicMix(mono, l_export);
                    BasicMix(mono, r_export);
                    break;
                }
            }

        return export_track;
    }

    private static void BasicMix(Memory<float> source, Memory<float> export)
    {
        // Hot path: a mixdown runs over the whole song on every incremental edit, so this
        // is vectorised like PcmEncoder.RenderSample and AudioMixer.Sum. Resolving
        // export.Span once instead of per iteration matters as much as the SIMD does.
        var src = source.Span;
        var dst = export.Span;

        var length = Math.Min(src.Length, dst.Length);
        var chunk_size = Vector<float>.Count;
        var chunked = length - length % chunk_size;

        for (var i = 0; i < chunked; i += chunk_size)
        {
            var slice = dst.Slice(i, chunk_size);
            (new Vector<float>(src.Slice(i, chunk_size)) + new Vector<float>(slice)).CopyTo(slice);
        }

        for (var i = chunked; i < length; i++) dst[i] += src[i];
    }
}