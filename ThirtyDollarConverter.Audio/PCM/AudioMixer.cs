using System.Collections.Concurrent;
using ThirtyDollarEncoder.Mixers;

namespace ThirtyDollarEncoder.PCM;

public class AudioMixer
{
    private readonly AudioLayout DefaultLayout;
    private readonly int Length;
    public readonly IMixingMethod MixingMethod = new BasicMixer();
    private readonly ConcurrentDictionary<(string, AudioLayout audio_layout), AudioData<float>> Tracks = new();

    public AudioMixer(AudioData<float> default_channel, AudioLayout default_layout = AudioLayout.Audio_LR)
    {
        Tracks.TryAdd((string.Empty, default_layout), default_channel);
        DefaultLayout = default_layout;
        Length = default_channel.GetLength();
    }

    public AudioData<float> MixDown()
    {
        var tracks = GetTracks();
        if (tracks.Length < 1) return tracks[0].Item2;

        var mixed = MixingMethod.MixTracks(tracks);
        return mixed;
    }

    public (AudioLayout audio_layout, AudioData<float> audio_data)[] GetTracks()
    {
        lock (Tracks)
        {
            var array = new (AudioLayout, AudioData<float>)[Tracks.Count];
            var i = 0;
            foreach (var ((_, layout), audio_data) in Tracks) array[i++] = (layout, audio_data);

            return array;
        }
    }

    public bool HasTrack(string sound, AudioLayout layout = AudioLayout.Audio_LR)
    {
        lock (Tracks)
        {
            return Tracks.ContainsKey((sound, layout));
        }
    }

    public AudioData<float> GetTrackOrDefault(string track_name, AudioLayout layout = AudioLayout.Audio_LR)
    {
        lock (Tracks)
        {
            return Tracks.TryGetValue((track_name, layout), out var found_channel)
                ? found_channel
                : Tracks[(string.Empty, DefaultLayout)];
        }
    }

    public AudioData<float> GetTrack(string track_name, AudioLayout layout = AudioLayout.Audio_LR)
    {
        lock (Tracks)
        {
            if (!Tracks.TryGetValue((track_name, layout), out var found_channel))
                throw new ArgumentException(
                    $"Unable to find track: \'{track_name}\' with layout: \'{layout}\'");

            return found_channel;
        }
    }

    public bool AddTrack(string track_name, AudioData<float> audio_data, AudioLayout layout = AudioLayout.Audio_LR)
    {
        if (audio_data.GetLength() != Length)
            throw new Exception("Added track doesn't have the same length as the default track.");

        lock (Tracks)
        {
            return Tracks.TryAdd((track_name, layout), audio_data);
        }
    }

    public AudioData<float> GetDefault()
    {
        lock (Tracks)
        {
            return Tracks[(string.Empty, DefaultLayout)];
        }
    }

    public int GetLength()
    {
        return Length;
    }
}

public enum AudioLayout
{
    /// <summary>
    ///     Uses only one channel of the AudioData&lt;float&gt;. Assumes the channel is left only.
    /// </summary>
    Audio_L,

    /// <summary>
    ///     Uses only one channel of the AudioData&lt;float&gt;. Assumes the channel is right only.
    /// </summary>
    Audio_R,

    /// <summary>
    ///     Uses only one channel of the AudioData&lt;float&gt;. Outputs to both LR.
    /// </summary>
    Audio_Mono,

    /// <summary>
    ///     Uses two channels of the AudioData&lt;float&gt;. Outputs to both LR.
    /// </summary>
    Audio_LR
}