using Serilog;
using Shared.Audio;
using System.Collections.Concurrent;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Encoder.Wave;

namespace EditorScene;

/// <summary>
///     The audio half of the wave reference tracks: every <see cref="WaveTrack" /> clip plays
///     on a buffer of its own, kept in step with the rendered mix once a frame. The mix is
///     never touched - it is re-rendered on a debounce after every edit, and a full-length copy
///     per render is what this avoids - so alignment is corrective rather than sample-exact,
///     within <see cref="SyncToleranceSeconds" />.
///     Shares its shape with <see cref="BackingAudio" />, the visualizer's single backing track,
///     but not its code: a clip here starts at an offset, carries its own gain, follows mute and
///     solo, and holds still outside its own span.
/// </summary>
public sealed class WavePlayback(AudioContext context, ILogger logger, Action<string> onError)
{
    /// <summary>
    ///     How far a clip may sit from where the transport says it should be before it is
    ///     seeked back. Every correction is a hard seek, so this trades an audible click
    ///     against audible drift; the visualizer's backing track allows 50 ms, which is too
    ///     slack for something notes get aligned against.
    /// </summary>
    private const double SyncToleranceSeconds = 0.015;

    /// <summary>How many peaks a file's envelope is summarized into. See <see cref="PeaksOf" />.</summary>
    private const int PeakBuckets = 2048;

    private readonly Dictionary<TrackPlacement, Clip> _clips = [];
    private readonly ConcurrentQueue<(string Path, WaveFile? File)> _decoded = new();
    private readonly Dictionary<string, WaveFile> _files = [];

    /// <summary>Paths that failed to decode; retried only when the editor asks for them again.</summary>
    private readonly HashSet<string> _failed = [];

    private readonly HashSet<string> _reading = [];
    private readonly HashSet<TrackPlacement> _seen = [];

    /// <summary>
    ///     Where the file should be, in seconds, when the transport's clock reads
    ///     <paramref name="playerSeconds" />. Null while the playhead is outside the clip -
    ///     before it starts, or past the end of the file.
    ///     The clip's start carries <see cref="EditorPlayback.LeadInSeconds" /> because the
    ///     rendered buffer does: a clip at quarter 0 has to sound when a note at quarter 0
    ///     does, which is 0.2 s into the buffer, not at its start.
    /// </summary>
    public static double? FilePosition(double playerSeconds, double startQuarterNotes, double bpm,
        double durationSeconds)
    {
        if (bpm <= 0 || durationSeconds <= 0) return null;

        var position = playerSeconds - (EditorPlayback.LeadInSeconds + startQuarterNotes / bpm * 60);
        return position < 0 || position > durationSeconds ? null : position;
    }

    /// <summary>
    ///     Fired on the update thread when a file has finished decoding, since everything
    ///     drawn from it - the clip's peak envelope - was drawn from nothing until then.
    /// </summary>
    public Action? OnFileDecoded { get; set; }

    /// <summary>
    ///     The file's peak envelope, or null while it is still being read - one normalized
    ///     value per bucket, evenly spaced over the whole file, which is what the arrangement
    ///     draws inside the clip.
    /// </summary>
    public float[]? Peaks(string path)
    {
        return _files.GetValueOrDefault(path)?.Peaks;
    }

    /// <summary>
    ///     Decodes a file (or answers from the cache) and reports its length in seconds; null
    ///     when it could not be read. The decode runs off the calling thread - a long file is a
    ///     visible hitch otherwise - and the result is picked up by the next <see cref="Sync" />.
    /// </summary>
    public async Task<double?> Prepare(string path)
    {
        if (_files.TryGetValue(path, out var cached)) return cached.Seconds;

        _failed.Remove(path);
        var file = await Task.Run(() => Read(path));
        _decoded.Enqueue((path, file));
        return file?.Seconds;
    }

    /// <summary>
    ///     Call once a frame on the update thread, whether or not anything is playing: a paused
    ///     or seeking transport still moves its clips, so that resuming is instant rather than
    ///     a frame late.
    /// </summary>
    public void Sync(ThirtyDollarProject project, Func<int, bool> isChannelAudible, double playerSeconds,
        bool playing, float masterVolume)
    {
        while (_decoded.TryDequeue(out var done))
        {
            _reading.Remove(done.Path);
            if (done.File is { } file)
            {
                _files[done.Path] = file;
                OnFileDecoded?.Invoke();
            }
            else
            {
                _failed.Add(done.Path);
            }
        }

        _seen.Clear();
        foreach (var placement in project.Placements)
        {
            if (placement.Track is not WaveTrack wave) continue;
            if (!_files.TryGetValue(wave.Path, out var file))
            {
                BeginRead(wave.Path);
                continue;
            }

            _seen.Add(placement);
            var clip = ClipFor(placement, file);
            var position = FilePosition(playerSeconds, placement.StartQuarterNotes, project.RootTiming.BPM,
                file.Seconds);

            clip.Drive(position, playing && position.HasValue && isChannelAudible(placement.Channel),
                (float)(wave.Volume / 100) * masterVolume);
        }

        if (_clips.Count == _seen.Count) return;
        foreach (var (placement, clip) in _clips.Where(pair => !_seen.Contains(pair.Key)).ToArray())
        {
            clip.Delete();
            _clips.Remove(placement);
        }
    }

    /// <summary>Silences every clip now, rather than on the next frame's sync.</summary>
    public void StopAll()
    {
        foreach (var clip in _clips.Values) clip.Drive(null, false, 0);
    }

    /// <summary>Drops every buffer and every decoded file. The editor is done with them.</summary>
    public void Dispose()
    {
        foreach (var clip in _clips.Values) clip.Delete();
        _clips.Clear();

        foreach (var file in _files.Values) file.Data.Dispose();
        _files.Clear();
    }

    // Test seams (internal - see EditorAssembly's InternalsVisibleTo("EditorScene.Tests")).
    internal int ClipCount => _clips.Count;

    /// <summary>Whether a clip is currently sounding; false for a placement that has none.</summary>
    internal bool IsSounding(TrackPlacement placement)
    {
        return _clips.TryGetValue(placement, out var clip) && clip.Sounding;
    }

    /// <summary>Where a clip's buffer sits, in seconds; null for a placement that has none.</summary>
    internal double? PositionSeconds(TrackPlacement placement)
    {
        return _clips.TryGetValue(placement, out var clip) ? clip.PositionSeconds : null;
    }

    private Clip ClipFor(TrackPlacement placement, WaveFile file)
    {
        if (_clips.TryGetValue(placement, out var existing) && existing.File == file) return existing;

        existing?.Delete(); // the clip's track points at another file now
        var clip = new Clip(context.GetBufferObject(file.Data, file.SampleRate), file);
        _clips[placement] = clip;
        return clip;
    }

    private void BeginRead(string path)
    {
        if (path.Length == 0 || _failed.Contains(path) || !_reading.Add(path)) return;
        Task.Run(() => _decoded.Enqueue((path, Read(path))));
    }

    /// <summary>
    ///     Decodes one file to float samples. Null (with the failure reported once) when the
    ///     path is gone or the decoder cannot read it - the clip then draws at its saved length
    ///     and stays silent, rather than the project failing to load.
    /// </summary>
    private WaveFile? Read(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var pcm = new WaveDecoder().Read(stream);
            // Mono has to be upmixed here: BassBuffer uploads every sample as stereo.
            var data = pcm.ReadAsFloat32Array(true);
            // A decode that yields nothing is a failure, not a zero-second reference: a
            // format the decoder cannot follow otherwise lands as a silent sliver of a clip
            // with no clue as to why.
            if (data is null || data.GetLength() == 0)
                throw new InvalidDataException("The file holds no readable audio.");

            return new WaveFile(data, (int)pcm.SampleRate, data.GetLength() / (double)pcm.SampleRate,
                PeaksOf(data));
        }
        catch (Exception e)
        {
            logger.Error("[Editor Playback] Couldn't read wave file \"{Path}\": {Exception}", path, e);
            onError($"Couldn't read \"{Path.GetFileName(path)}\":\n{e.Message}");
            return null;
        }
    }

    /// <summary>
    ///     The loudest sample in each of <see cref="PeakBuckets" /> even slices of the file,
    ///     normalized to the loudest of them so a quiet recording still draws. Computed once,
    ///     on the decode thread, at a fixed resolution rather than per zoom level - 2048 floats
    ///     is 8 KB a file, and the arrangement samples whichever bucket a pixel lands in.
    ///     // ponytail: channel 0 only. A stereo file whose channels differ wildly draws the
    ///     left one; take the max of both if that ever misleads.
    /// </summary>
    private static float[] PeaksOf(AudioData<float> data)
    {
        var samples = data.GetChannel(0);
        var peaks = new float[PeakBuckets];
        if (samples.Length == 0) return peaks;

        var loudest = 0f;
        for (var bucket = 0; bucket < PeakBuckets; bucket++)
        {
            var start = (int)((long)bucket * samples.Length / PeakBuckets);
            var end = (int)((long)(bucket + 1) * samples.Length / PeakBuckets);

            var peak = 0f;
            for (var i = start; i < end; i++) peak = Math.Max(peak, Math.Abs(samples[i]));
            peaks[bucket] = peak;
            loudest = Math.Max(loudest, peak);
        }

        if (loudest <= 0) return peaks;
        for (var bucket = 0; bucket < PeakBuckets; bucket++) peaks[bucket] /= loudest;
        return peaks;
    }

    /// <summary>
    ///     One decoded file, shared by every clip that plays it.
    ///     // ponytail: never evicted while the editor is open - a project holds a handful of
    ///     references, not a library. Evict by use count if that stops being true.
    /// </summary>
    private sealed record WaveFile(AudioData<float> Data, int SampleRate, double Seconds, float[] Peaks);

    /// <summary>
    ///     One placement's buffer. It is played once at creation and paused immediately, so a
    ///     channel exists to seek: <see cref="AudibleBuffer.Play" /> starts a *new* channel on
    ///     every call, and calling it per resume would layer the file over itself.
    /// </summary>
    private sealed class Clip
    {
        private readonly AudibleBuffer _buffer;
        private bool _playing;
        private float _volume = -1;

        public Clip(AudibleBuffer buffer, WaveFile file)
        {
            _buffer = buffer;
            File = file;

            buffer.SetVolume(0); // silent until the first Drive, so priming makes no sound
            buffer.Play();
            buffer.SetPause(true);
        }

        public WaveFile File { get; }

        public bool Sounding => _playing;

        public double PositionSeconds => _buffer.GetTime_Milliseconds() / 1000d;

        public void Drive(double? position, bool play, float volume)
        {
            if (Math.Abs(volume - _volume) > 0.0005f)
            {
                _buffer.SetVolume(volume);
                _volume = volume;
            }

            // Seeked before the pause state changes, so a resume starts from the right sample.
            if (position is { } seconds &&
                Math.Abs(_buffer.GetTime_Milliseconds() / 1000d - seconds) > SyncToleranceSeconds)
                _buffer.SeekTime_Milliseconds((long)(seconds * 1000));

            if (play == _playing) return;
            _buffer.SetPause(!play);
            _playing = play;
        }

        public void Delete()
        {
            _buffer.Stop();
            _buffer.Delete();
        }
    }
}
