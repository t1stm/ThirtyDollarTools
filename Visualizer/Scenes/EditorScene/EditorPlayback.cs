using System.Diagnostics;
using Shared;
using Shared.Audio;
using Shared.Objects;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Objects;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarEncoder.Resamplers;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;

namespace EditorScene;

/// <summary>
///     Playback keeps one persistent render of the mute-filtered project (<see cref="_rendered" />),
///     updated incrementally through PCMEncoder's native path - the same one
///     <see cref="ExportWav" /> uses - on every model edit and mute toggle. Soloing renders
///     a second, independent buffer (<see cref="_soloRendered" />) for just the soloed
///     channels and plays that instead, while the full mix keeps getting updated underneath
///     it (so unsoloing is an instant swap back, never a re-render). Unsoloing discards the
///     solo buffer. Memory is bounded: one full buffer always, plus one solo buffer only
///     while a channel is soloed - never O(track count).
/// </summary>
public class EditorPlayback
{
    private const int DebounceMs = 250;

    /// <summary>
    ///     Every TDW sequence starts one step at the initial 300 BPM before its first
    ///     "!speed" (site behavior, see PlacementCalculator): rendered time =
    ///     arrangement time + this. Pinned by ChannelSequenceTests.
    /// </summary>
    public const double LeadInSeconds = 0.2;

    private readonly Stopwatch _sinceEdit = new();
    private readonly EditorState _state;
    private readonly ThirtyDollarWorkflow _workflow;

    private readonly SampleProcessor _previewProcessor;
    private readonly uint _previewSampleRate;
    private readonly List<AudibleBuffer> _preview = [];

    private bool _modelDirty;
    private bool _remixPending;
    private bool _rendering;
    private bool _playWhenReady;
    private TimedEvents? _timedEvents;
    private RenderedSequence? _rendered;
    private RenderedSequence? _soloRendered;

    private float _statusProgress;
    private ulong _statusDone;
    private ulong _statusTotal;
    private string? _lastAlertedError;

    public EditorPlayback(ThirtyDollarWorkflow workflow, EditorState state)
    {
        _workflow = workflow;
        _state = state;
        Encoder = new PcmEncoder(workflow.SampleHolder, workflow.EncoderSettings,
            indexReport: (done, total) =>
            {
                _statusDone = done;
                _statusTotal = total;
                _statusProgress = total == 0 ? 0f : (float)done / total;
            });

        // Previews favor latency over quality: the cheapest resampler there is.
        var previewSettings = new EncoderSettings
        {
            SampleRate = workflow.EncoderSettings.SampleRate,
            Channels = workflow.EncoderSettings.Channels,
            Resampler = new LinearResampler()
        };
        _previewSampleRate = previewSettings.SampleRate;
        _previewProcessor = new SampleProcessor(workflow.SampleHolder.SampleList, previewSettings);
    }

    /// <summary>
    ///     Whether placing/moving notes previews their sound while the song is playing.
    ///     ponytail: plain flag for now, a settings menu will own it later.
    /// </summary>
    public bool PreviewDuringPlayback { get; set; }

    private PcmEncoder Encoder { get; }

    /// <summary>What the encoder is currently doing, for the inspector's status bar; null = idle.
    /// Written on background render/export threads, read once a frame on the update thread -
    /// plain field, no lock (see EditorPlayback's class doc for the polling rationale).
    /// // ponytail: single status slot; per-operation lanes if concurrent encodes (a re-render
    /// racing an export, both on the same Encoder) ever need to show independent progress.</summary>
    public string? StatusLabel { get; private set; }

    public float StatusProgress => _statusProgress;

    /// <summary>The encoder's last "done" / "total" counts from <see cref="StatusProgress" />'s
    /// same report - the inspector's status label shows these in brackets. Both 0 when the
    /// current phase hasn't reported yet (placement/mixing report nothing) or nothing is running.</summary>
    public ulong StatusDone => _statusDone;

    public ulong StatusTotal => _statusTotal;

    /// <summary>Set when a render or export fails; consume with <see cref="TakeError" /> to show
    /// one dialog per failure.</summary>
    public string? PendingError { get; private set; }

    /// <summary>Returns the pending error, if any, and clears it - one failure shows one dialog.</summary>
    public string? TakeError()
    {
        var error = PendingError;
        PendingError = null;
        return error;
    }

    public bool IsPlaying => _workflow.SequencePlayer.GetTimingStopwatch().IsRunning;
    public long ElapsedMs => _workflow.SequencePlayer.GetTimingStopwatch().ElapsedMilliseconds;

    public long TotalMs
    {
        get
        {
            var placement = _timedEvents?.Placement;
            if (placement is not { Length: > 0 }) return 0;
            return (long)placement[^1].Index * 1000 / _timedEvents!.TimingSampleRate;
        }
    }

    /// <summary>Playhead position on the arrangement timeline; negative during the lead-in.</summary>
    public double PlayheadQuarters => (ElapsedMs / 1000d - LeadInSeconds) / 60d * _state.Project.RootTiming.BPM;

    /// <summary>True once a buffer was rendered and uploaded - the transport is live.</summary>
    public bool HasSession => _timedEvents != null;

    /// <summary>Model changed: re-render (debounced) if a playback session exists.</summary>
    public void NotifyModelChanged()
    {
        _modelDirty = true;
        _sinceEdit.Restart();
    }

    /// <summary>Mute/solo changed: remix the cached channel renders.</summary>
    public void NotifyChannelsChanged()
    {
        _remixPending = true;
    }

    public void PlayPause()
    {
        StopPreview();
        if (_rendering)
        {
            _playWhenReady = true;
            return;
        }

        if (_timedEvents == null)
        {
            StartRender(true);
            return;
        }

        _workflow.SequencePlayer.TogglePause();
    }

    /// <summary>Restarts playback from the beginning (Shift+Space).</summary>
    public void Restart()
    {
        StopPreview();
        if (_rendering)
        {
            _playWhenReady = true;
            return;
        }

        if (_timedEvents == null)
        {
            StartRender(true);
            return;
        }

        var player = _workflow.SequencePlayer;
        player.Seek(0);
        if (!player.GetTimingStopwatch().IsRunning) player.TogglePause();
    }

    public void Stop()
    {
        StopPreview();
        _playWhenReady = false;
        var player = _workflow.SequencePlayer;
        if (player.GetTimingStopwatch().IsRunning) player.TogglePause();
        player.Seek(0);
    }

    /// <summary>
    ///     Seeks to an arrangement-timeline position (quarter notes at the root BPM) without
    ///     touching play/pause state - used by the ruler click-to-seek in both editor views.
    /// </summary>
    public void Seek(double quarters)
    {
        if (!HasSession) return;
        var ms = (long)(((quarters / _state.Project.RootTiming.BPM) * 60 + LeadInSeconds) * 1000);
        _workflow.SequencePlayer.Seek(Math.Clamp(ms, 0, TotalMs));
    }

    /// <summary>
    ///     Plays every sound of an instrument (a note being placed or moved), each combined
    ///     with its own <see cref="SoundAdjustment" /> if the instrument has one, replacing
    ///     any still-playing preview. Suppressed during playback unless
    ///     <see cref="PreviewDuringPlayback" /> is set. Empty instrument -> no preview.
    /// </summary>
    public void PreviewNote(Instrument instrument, double value)
    {
        if (IsPlaying && !PreviewDuringPlayback) return;

        StopPreview();
        foreach (var sound in instrument.Sounds)
        {
            var adjustment = instrument.Adjustments.GetValueOrDefault(sound);
            PlayOne(sound, adjustment?.CombineValue(value) ?? value,
                adjustment?.CombineVolume(null), adjustment?.CombinePan(0) ?? 0);
        }
    }

    /// <summary>Plays one sound with its adjustment applied on top of no base note (value 0,
    /// default volume/pan) - the instrument editor's per-sound preview, fired as the user
    /// scrolls a sound's value/volume/pan or hits its row's preview button. Replaces any
    /// still-playing preview, same suppression as <see cref="PreviewNote" />.</summary>
    public void PreviewSound(string sound, SoundAdjustment adjustment)
    {
        if (IsPlaying && !PreviewDuringPlayback) return;

        StopPreview();
        PlayOne(sound, adjustment.CombineValue(0), adjustment.CombineVolume(null), adjustment.CombinePan(0));
    }

    /// <summary>Plays every given sound layered together, each with its own adjustment on
    /// top of no base note - the instrument editor's "Preview" button, previewing the whole
    /// instrument as it would sound on a note at value 0.</summary>
    public void PreviewInstrument(IEnumerable<(string Sound, SoundAdjustment Adjustment)> sounds)
    {
        if (IsPlaying && !PreviewDuringPlayback) return;

        StopPreview();
        foreach (var (sound, adjustment) in sounds)
            PlayOne(sound, adjustment.CombineValue(0), adjustment.CombineVolume(null), adjustment.CombinePan(0));
    }

    private void PlayOne(string sound, double value, double? volume, float pan)
    {
        BaseEvent ev = pan == 0
            ? new NormalEvent { SoundEvent = sound, Value = value, WorkingValue = value, Volume = volume }
            : new ExtendedEvent { SoundEvent = sound, Value = value, WorkingValue = value, Volume = volume, Pan = pan };

        var audio = _previewProcessor.ProcessEvent(ev);
        if (audio.GetLength() == 0) return; // unknown sound or samples still downloading

        var buffer = _workflow.SequencePlayer.AudioContext.GetBufferObject(audio, (int)_previewSampleRate);
        // SampleProcessor only resamples for pitch - volume/pan are normally baked into PCM
        // during PCMEncoder's mixdown, which a one-shot preview buffer never goes through.
        // AudibleBuffer's own volume/pan (latched by Play(), see BassBuffer/OpenALBuffer) are
        // on 0-1/-1-1 scales, unlike the event's 0-100/-100-100 - convert both.
        buffer.SetVolume((float)(volume ?? 100) / 100f);
        buffer.SetPan(pan / 100f);
        buffer.Play();
        _preview.Add(buffer);
    }

    /// <summary>
    ///     Renders the merged project fresh and writes it as a WAV. Independent of the
    ///     playback session and of mute/solo - the export always contains every track.
    /// </summary>
    public Task ExportWav(string path)
    {
        // Snapshot on the update thread, render in the background (StartRender's rule).
        var merged = _state.Project.ToSequence();
        return Task.Run(async () =>
        {
            try
            {
                StatusLabel = "Rendering export…";
                _statusProgress = 0f;
                _statusDone = 0;
                _statusTotal = 0;
                var rendered = await Encoder.GetSequenceAudio(merged);

                StatusLabel = "Writing WAV…";
                _statusProgress = 0f;
                _statusDone = 0;
                _statusTotal = 0;
                Encoder.WriteAsWavFile(path, rendered.Audio);

                _lastAlertedError = null;
            }
            catch (Exception e)
            {
                _workflow.Logger.Error("[Editor Playback] WAV export failed: {Exception}", e);
                SetError($"WAV export failed:\n{e.Message}");
            }
            finally
            {
                StatusLabel = null;
            }
        });
    }

    public void StopPreview()
    {
        foreach (var buffer in _preview)
        {
            buffer.Stop();
            buffer.Delete();
        }

        _preview.Clear();
    }

    /// <summary>Call once per frame on the update thread.</summary>
    public void Update()
    {
        if (_rendering) return;

        if (_modelDirty && _sinceEdit.ElapsedMilliseconds >= DebounceMs) StartRender(false);
        else if (_remixPending) StartRender(false);
    }

    /// <summary>
    ///     Re-renders the mute-filtered full mix, and - while any channel is soloed - the
    ///     solo mix alongside it, then plays whichever is active. Model edits and mute
    ///     toggles keep the full mix's incremental diff cheap; a solo toggle's diff is
    ///     against the solo buffer, never the full one, and turning solo off entirely just
    ///     swaps playback back to the (already up to date) full buffer with no re-render.
    /// </summary>
    private void StartRender(bool startPlayback)
    {
        _rendering = true;
        _modelDirty = false;
        _remixPending = false;
        StatusLabel = "Rendering audio…";
        _statusProgress = 0f;
        _statusDone = 0;
        _statusTotal = 0;

        // Snapshotted here, on the update thread - the only thread that mutates the
        // model - so the background render works on a consistent state.
        var project = _state.Project;
        var full = project.ToSequence(c => !_state.IsMuted(c));
        var solo = _state.AnySoloed ? project.ToSequence(_state.IsSoloed) : null;

        Task.Run(async () =>
        {
            try
            {
                _rendered = _rendered != null
                    ? await Encoder.ComputeIncrementalAudio(_rendered, [full])
                    : await Encoder.GetSequenceAudio(full);

                if (solo != null)
                {
                    _soloRendered = _soloRendered != null
                        ? await Encoder.ComputeIncrementalAudio(_soloRendered, [solo])
                        : await Encoder.GetSequenceAudio(solo);

                    Upload(_soloRendered, startPlayback);
                }
                else
                {
                    _soloRendered?.Mixer?.Dispose();
                    _soloRendered = null;

                    Upload(_rendered, startPlayback);
                }

                _lastAlertedError = null;
            }
            catch (Exception e)
            {
                _workflow.Logger.Error("[Editor Playback] Render failed: {Exception}", e);
                SetError($"Render failed:\n{e.Message}");
            }
            finally
            {
                _rendering = false;
                StatusLabel = null;
            }
        });
    }

    /// <summary>Sets <see cref="PendingError" /> unless it's a repeat of the last alerted
    /// message - an edit-storm where every debounced re-render fails would otherwise pop a
    /// dialog per edit. A successful render/export clears the memory so a later failure
    /// (even with the same message) alerts again.</summary>
    private void SetError(string message)
    {
        if (message == _lastAlertedError) return;
        _lastAlertedError = message;
        PendingError = message;
    }

    private void Upload(RenderedSequence rendered, bool startPlayback)
    {
        startPlayback = startPlayback || _playWhenReady;
        _playWhenReady = false;

        var player = _workflow.SequencePlayer;
        if (startPlayback) player.Stop();

        var events = rendered.TimedEvents;
        player.UpdateSequence(events, ThirtyDollarWorkflow.GenerateSequenceIndexes(events.Placement),
            rendered, startPlayback);
        _timedEvents = events;

        if (startPlayback) player.Start(_workflow.Game.ThreadRunner);
        else player.AlignToTime();
    }
}
