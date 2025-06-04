using System.Collections.Concurrent;
using ThirtyDollarEncoder.Mixers;

namespace ThirtyDollarEncoder.PCM;

public class AudioMixer
{
    private readonly AudioLayout _defaultLayout;
    private readonly int _length;
    private readonly ConcurrentDictionary<(string, AudioLayout audio_layout), AudioData<float>> _tracks = new();
    public readonly IMixingMethod MixingMethod = new BasicMixer();

    public AudioMixer(AudioData<float> defaultChannel, AudioLayout defaultLayout = AudioLayout.AudioLr)
    {
        _tracks.TryAdd((string.Empty, defaultLayout), defaultChannel);
        _defaultLayout = defaultLayout;
        _length = defaultChannel.GetLength();
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
        lock (_tracks)
        {
            var array = new (AudioLayout, AudioData<float>)[_tracks.Count];
            var i = 0;
            foreach (var ((_, layout), audio_data) in _tracks) array[i++] = (layout, audio_data);

            return array;
        }
    }

    public bool HasTrack(string sound, AudioLayout layout = AudioLayout.AudioLr)
    {
        lock (_tracks)
        {
            return _tracks.ContainsKey((sound, layout));
        }
    }

    public AudioData<float> GetTrackOrDefault(string trackName, AudioLayout layout = AudioLayout.AudioLr)
    {
        lock (_tracks)
        {
            return _tracks.TryGetValue((trackName, layout), out var found_channel)
                ? found_channel
                : _tracks[(string.Empty, _defaultLayout)];
        }
    }

    public AudioData<float> GetTrack(string trackName, AudioLayout layout = AudioLayout.AudioLr)
    {
        lock (_tracks)
        {
            if (!_tracks.TryGetValue((trackName, layout), out var found_channel))
                throw new ArgumentException(
                    $"Unable to find track: \'{trackName}\' with layout: \'{layout}\'");

            return found_channel;
        }
    }

    public bool AddTrack(string trackName, AudioData<float> audioData, AudioLayout layout = AudioLayout.AudioLr)
    {
        if (audioData.GetLength() != _length)
            throw new Exception("Added track doesn't have the same length as the default track.");

        lock (_tracks)
        {
            return _tracks.TryAdd((trackName, layout), audioData);
        }
    }

    public AudioData<float> GetDefault()
    {
        lock (_tracks)
        {
            return _tracks[(string.Empty, _defaultLayout)];
        }
    }

    public int GetLength()
    {
        return _length;
    }
}

public enum AudioLayout
{
    /// <summary>
    /// Uses only one channel of the <see cref="AudioData{T}" />. Assumes the channel is left only.
    /// </summary>
    AudioL,

    /// <summary>
    /// Uses only one channel of the <see cref="AudioData{T}" />. Assumes the channel is right only.
    /// </summary>
    AudioR,

    /// <summary>
    /// Uses only one channel of the <see cref="AudioData{T}" />. Outputs to both LR.
    /// </summary>
    AudioMono,

    /// <summary>
    /// Uses two channels of the <see cref="AudioData{T}" />. Outputs to both LR.
    /// </summary>
    AudioLr
}