using System.Collections.Concurrent;
using System.Numerics;
using ThirtyDollarEncoder.Mixers;

namespace ThirtyDollarEncoder.PCM;

public class AudioMixer : IDisposable
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

    public void Dispose()
    {
        foreach (var (_, audioData) in _tracks) audioData.Dispose();
        GC.SuppressFinalize(this);
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

    /// <summary>
    ///     The keys of every named (non-default) track - the separated per-sound channels a
    ///     cut targets. Used to mirror a mixer's track set onto an incremental overlay, so
    ///     sounds route to the same track in both and <see cref="Sum" /> merges them in place.
    /// </summary>
    public (string Name, AudioLayout Layout)[] GetNamedTrackKeys()
    {
        lock (_tracks)
        {
            return [.. _tracks.Keys.Where(key => key.Item1.Length > 0)];
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

    /// <summary>
    ///     Sums / add the channels of other mixers to the current one.
    /// </summary>
    /// <param name="addMixer">An array of AudioMixer to sum with the current one.</param>
    public void Sum(params ReadOnlySpan<AudioMixer> addMixer)
    {
        lock (_tracks)
        {
            foreach (var mixer in addMixer)
            {
                if (mixer.GetLength() > _length)
                    throw new Exception("Trying to sum a mixer that has bigger length than the current one.");

                foreach (var ((trackName, layout), audioData) in mixer._tracks)
                {
                    if (!_tracks.TryGetValue((trackName, layout), out var existingData))
                    {
                        _tracks.TryAdd((trackName, layout), audioData);
                        continue;
                    }

                    for (var i = 0; i < existingData.Samples.Length; i++)
                    {
                        var existingChannel = existingData.Samples[i].AsSpan();
                        var incomingChannel = audioData.Samples[i].AsSpan();

                        var chunkSize = Vector<float>.Count;
                        var minLength = Math.Min(existingChannel.Length, incomingChannel.Length);
                        var chunked = minLength - minLength % chunkSize;

                        for (var j = 0; j < chunked; j += chunkSize)
                        {
                            var existingVector = new Vector<float>(existingChannel.Slice(j, chunkSize));
                            var incomingVector = new Vector<float>(incomingChannel.Slice(j, chunkSize));

                            (existingVector + incomingVector).CopyTo(existingChannel.Slice(j, chunkSize));
                        }

                        for (var j = chunked; j < minLength; j++) existingChannel[j] += incomingChannel[j];
                    }
                }
            }
        }
    }
}