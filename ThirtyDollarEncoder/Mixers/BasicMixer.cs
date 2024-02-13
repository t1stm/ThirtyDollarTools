using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarEncoder.Mixers;

public class BasicMixer : IMixingMethod
{
    public AudioData<float> MixTracks((AudioLayout, AudioData<float>)[] tracks)
    {
        var length = tracks[0].Item2.GetLength();
        var export_track = AudioData<float>.WithLength(2, length);
        
        foreach (var (layout, audio_data) in tracks)
        {
            switch (layout)
            {
                case AudioLayout.Audio_L:
                {
                    var left = audio_data.GetChannel(0);
                    var left_export = export_track.GetChannel(0);

                    BasicMix(left, left_export);
                    break;
                }
                
                case AudioLayout.Audio_R:
                {
                    var right = audio_data.GetChannel(0);
                    var right_export = export_track.GetChannel(0);

                    BasicMix(right, right_export);
                    break;
                }
                
                case AudioLayout.Audio_LR:
                {
                    var l = audio_data.GetChannel(0);
                    var l_export = export_track.GetChannel(0);

                    var r = audio_data.GetChannel(1);
                    var r_export = export_track.GetChannel(1);

                    BasicMix(l, l_export);
                    BasicMix(r, r_export);
                    break;
                }
                
                case AudioLayout.Audio_Mono:
                {
                    var mono = audio_data.GetChannel(0);
                    var l_export = export_track.GetChannel(0);
                    var r_export = export_track.GetChannel(1);
                    
                    BasicMix(mono, l_export);
                    BasicMix(mono, r_export);
                    break;
                }
            }
        }

        return export_track;
    }

    private static void BasicMix(Memory<float> source, Memory<float> export)
    {
        var span = source.Span;
        for (var i = 0; i < span.Length; i++)
        {
            export.Span[i] += span[i];
        }
    }
}