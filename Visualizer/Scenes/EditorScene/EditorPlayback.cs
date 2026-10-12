using EditorScene.State;
using System.Diagnostics;
using Shared;
using Shared.Audio;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Encoder.Resamplers;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace EditorScene;

/// <summary>
///     Owns the editor's audio session. Holds one persistent render of the mute-filtered
///     project (<see cref="_rendered" />), updated incrementally through PcmEncoder on every
///     model edit and mute toggle. While a channel is soloed a second buffer
///     (<see cref="_soloRendered" />) holds just the soloed channels and plays instead, with
///     the full mix still updated underneath it, so unsoloing swaps back without a
///     re-render and discards the solo buffer.
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

    private readonly List<AudibleBuffer> _preview = [];

    private readonly PcmEncoder _previewEncoder;
    private readonly PlacementCalculator _previewPlacement;
    private readonly SampleProcessor _previewProcessor;
    private readonly uint _previewSampleRate;

    private readonly Stopwatch _sinceEdit = new();
    private readonly EditorState _state;
    private readonly WavePlayback _wave;
    private readonly ThirtyDollarWorkflow _workflow;

    /// <summary>
    ///     The last combo <see cref="PreviewInstrument(IEnumerable{InstrumentSound})" /> rendered,
    ///     kept so previewing it again plays at once. One entry; cleared by
    ///     <see cref="ClearPreviewCache" /> when the instrument editor closes.
    /// </summary>
    private Task<AudioData<float>>? _combo;

    private (string Sound, double Value, double? Volume, float Pan)[] _comboKey = [];

    /// <summary>Set while a requested combo preview waits on its render; <see cref="Update" /> plays it.</summary>
    private bool _comboPlayPending;

    private string? _lastAlertedError;

    private bool _modelDirty;
    private bool _playWhenReady;
    private bool _remixPending;
    private RenderedSequence? _rendered;
    private bool _rendering;
    private RenderedSequence? _soloRendered;

    private TimedEvents? _timedEvents;

    public EditorPlayback(ThirtyDollarWorkflow workflow, EditorState state)
    {
        _workflow = workflow;
        _state = state;
        _wave = new WavePlayback(workflow.SequencePlayer.AudioContext, workflow.Logger, SetError);
        Encoder = new PcmEncoder(workflow.SampleHolder, workflow.EncoderSettings,
            indexReport: (done, total) =>
            {
                StatusDone = done;
                StatusTotal = total;
                StatusProgress = total == 0 ? 0f : (float)done / total;
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
        // Its own encoder, so a preview never reports into the status bar or queues behind a re-render.
        _previewEncoder = new PcmEncoder(workflow.SampleHolder, previewSettings);
        _previewPlacement = new PlacementCalculator(previewSettings);
    }

    /// <summary>
    ///     Whether placing/moving notes previews their sound while the song is playing.
    ///     ponytail: plain flag for now, a settings menu will own it later.
    /// </summary>
    public bool PreviewDuringPlayback { get; set; }

    private PcmEncoder Encoder { get; }

    /// <summary>
    ///     What the encoder is currently doing, for the inspector's status bar; null = idle.
    ///     Written on background render/export threads and read once a frame on the update
    ///     thread - a plain field, no lock, since the reader only polls it.
    ///     // ponytail: single status slot; per-operation lanes if concurrent encodes (a re-render
    ///     racing an export, both on the same Encoder) ever need independent progress.
    /// </summary>
    public string? StatusLabel { get; private set; }

    public float StatusProgress { get; private set; }

    /// <summary>
    ///     The encoder's last "done" / "total" counts from <see cref="StatusProgress" />'s
    ///     same report - the inspector's status label shows these in brackets. Both 0 when the
    ///     current phase hasn't reported yet (placement/mixing report nothing) or nothing is running.
    /// </summary>
    public ulong StatusDone { get; private set; }

    public ulong StatusTotal { get; private set; }

    /// <summary>
    ///     Set when a render or export fails; consume with <see cref="TakeError" /> to show
    ///     one dialog per failure.
    /// </summary>
    public string? PendingError { get; private set; }

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

    /// <summary>Returns the pending error, if any, and clears it - one failure shows one dialog.</summary>
    public string? TakeError()
    {
        var error = PendingError;
        PendingError = null;
        return error;
    }

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
        _wave.StopAll();
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
        var ms = (long)((quarters / _state.Project.RootTiming.BPM * 60 + LeadInSeconds) * 1000);
        _workflow.SequencePlayer.Seek(Math.Clamp(ms, 0, TotalMs));
    }

    /// <summary>
    ///     Plays a note as it will actually sound: every sound of its instrument, each
    ///     combining the note's value, volume and pan with its own
    ///     <see cref="InstrumentSound" /> tuning. Replaces any still-playing preview;
    ///     suppressed during playback unless <see cref="PreviewDuringPlayback" /> is set.
    ///     Empty instrument (or a cut, which has no sound of its own) -> no preview.
    /// </summary>
    /// <param name="valueOverride">
    ///     Value to play at instead of the note's own - a drag previews where the note is
    ///     heading, one frame before it lands there.
    /// </param>
    public void PreviewNote(Note note, double? valueOverride = null)
    {
        if (IsPlaying && !PreviewDuringPlayback) return;

        StopPreview();
        if (note.IsCut) return;

        var value = valueOverride ?? note.Value;
        foreach (var sound in note.Instrument.Sounds)
            PlayOne(sound.Sound, sound.CombineValue(value), sound.CombineVolume(note.Volume),
                sound.CombinePan(note.Pan));
    }

    /// <summary>
    ///     Every sound of an instrument at its own tuning, on no note - the palette's
    ///     right-click preview, where there is no note to take a volume or pan from.
    /// </summary>
    public void PreviewInstrument(Instrument instrument)
    {
        PreviewInstrument(instrument.Sounds);
    }

    /// <summary>
    ///     Plays one sound with its adjustment applied on top of no base note (value 0,
    ///     default volume/pan) - the instrument editor's per-sound preview, fired as the user
    ///     scrolls a sound's value/volume/pan or hits its row's preview button. Replaces any
    ///     still-playing preview, same suppression as <see cref="PreviewNote" />.
    /// </summary>
    public void PreviewSound(InstrumentSound sound)
    {
        if (IsPlaying && !PreviewDuringPlayback) return;

        StopPreview();
        PlayOne(sound.Sound, sound.CombineValue(0), sound.CombineVolume(null), sound.CombinePan(0));
    }

    /// <summary>
    ///     Plays every given sound layered together, each with its own adjustment on
    ///     top of no base note - the instrument editor's "Preview" button, previewing the whole
    ///     instrument as it would sound on a note at value 0. Mixed by PcmEncoder into one
    ///     buffer, as a render would, rather than one buffer per sound started side by side.
    ///     The render runs in the background and <see cref="Update" /> plays it once it lands;
    ///     previewing the same combo again replays the cached mix.
    /// </summary>
    public void PreviewInstrument(IEnumerable<InstrumentSound> sounds)
    {
        if (IsPlaying && !PreviewDuringPlayback) return;

        StopPreview();
        (string, double, double?, float)[] combo =
            [.. sounds.Select(s => (s.Sound, s.CombineValue(0), s.CombineVolume(null), s.CombinePan(0)))];
        if (combo.Length == 0) return;

        if (_combo == null || !combo.SequenceEqual(_comboKey))
        {
            _comboKey = combo;
            _combo = RenderCombo(_previewEncoder, _previewPlacement, _previewSampleRate, combo);
        }

        _comboPlayPending = true;
        TryPlayCombo();
    }

    /// <summary>Drops the cached combo render - the instrument editor calls this as it closes.</summary>
    public void ClearPreviewCache()
    {
        _combo = null;
        _comboKey = [];
        _comboPlayPending = false;
    }

    /// <summary>
    ///     Mixes a combo into one buffer that starts with its sounds. Static and internal so the
    ///     mix can be pinned without an audio device - see EditorScene.Tests.
    /// </summary>
    internal static Task<AudioData<float>> RenderCombo(PcmEncoder encoder, PlacementCalculator calculator,
        uint sampleRate, (string Sound, double Value, double? Volume, float Pan)[] combo)
    {
        // Layered on one step and "!combine"-joined, the way a note's sounds are.
        var events = new List<BaseEvent>();
        foreach (var (sound, value, volume, pan) in combo)
        {
            if (events.Count > 0) events.Add(new NormalEvent { SoundEvent = "!combine", ValueScale = ValueScale.None });
            events.Add(PreviewEvent(sound, value, volume, pan));
        }

        var sequence = new Sequence { Events = [.. events] };

        return Task.Run(() =>
        {
            // Placed from index 0: a full render opens on a lead-in step (LeadInSeconds),
            // which would be dead air in front of a preview.
            var placement = calculator.CalculateOne(sequence, 0).OrderBy(p => p.Index).ToArray();
            return encoder.GetAudioFromTimedEvents(new TimedEvents
            {
                Sequences = [sequence],
                Placement = placement,
                TimingSampleRate = (int)sampleRate
            });
        });
    }

    /// <summary>Plays the pending combo once its render has landed; a no-op until then.</summary>
    private void TryPlayCombo()
    {
        if (_combo is not { IsCompleted: true } done) return;
        _comboPlayPending = false;

        if (!done.IsCompletedSuccessfully)
        {
            _workflow.Logger.Error("[Editor Playback] Preview render failed: {Exception}", done.Exception);
            _combo = null; // the next preview retries instead of replaying the failure
            return;
        }

        var audio = done.Result;
        if (audio.GetLength() == 0)
        {
            _combo = null; // unknown sounds or samples still downloading - retry next time
            return;
        }

        var buffer = _workflow.SequencePlayer.AudioContext.GetBufferObject(audio, (int)_previewSampleRate);
        buffer.SetVolume(1f); // volume and pan are already mixed in
        buffer.Play();
        _preview.Add(buffer);
    }

    private static BaseEvent PreviewEvent(string sound, double value, double? volume, float pan)
    {
        return pan == 0
            ? new NormalEvent { SoundEvent = sound, Value = value, WorkingValue = value, Volume = volume }
            : new ExtendedEvent { SoundEvent = sound, Value = value, WorkingValue = value, Volume = volume, Pan = pan };
    }

    private void PlayOne(string sound, double value, double? volume, float pan)
    {
        var audio = _previewProcessor.ProcessEvent(PreviewEvent(sound, value, volume, pan));
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
                StatusProgress = 0f;
                StatusDone = 0;
                StatusTotal = 0;
                var rendered = await Encoder.GetSequenceAudio(merged);

                StatusLabel = "Writing WAV…";
                StatusProgress = 0f;
                StatusDone = 0;
                StatusTotal = 0;
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

    /// <summary>
    ///     Decodes a wave file (or answers from the cache) and reports its length in seconds;
    ///     null when it could not be read, in which case the failure has already been raised as
    ///     an error. Adding a wave track needs the length before the clip can draw.
    /// </summary>
    public Task<double?> PrepareWave(string path)
    {
        return _wave.Prepare(path);
    }

    /// <summary>
    ///     Fired on the update thread when a wave file finishes decoding - whatever draws from
    ///     it has been drawing from nothing until now. See <see cref="WavePeaks" />.
    /// </summary>
    public Action? OnWaveDecoded
    {
        get => _wave.OnFileDecoded;
        set => _wave.OnFileDecoded = value;
    }

    /// <summary>
    ///     The peak envelope of an already-decoded wave file, for the clip the arrangement
    ///     draws; null until the decode lands.
    /// </summary>
    public float[]? WavePeaks(string path)
    {
        return _wave.Peaks(path);
    }

    public void StopPreview()
    {
        _comboPlayPending = false;
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
        if (_comboPlayPending) TryPlayCombo();

        // Ahead of the render guard: the wave references follow the transport's clock, which
        // keeps running while a re-render is in flight.
        _wave.Sync(_state.Project, _state.IsChannelAudible, ElapsedMs / 1000d, IsPlaying,
            _workflow.SequencePlayer.Volume);

        if (_rendering) return;

        if (_modelDirty && _sinceEdit.ElapsedMilliseconds >= DebounceMs) StartRender(false);
        else if (_remixPending) StartRender(false);
    }

    /// <summary>
    ///     Re-renders the mute-filtered full mix, plus the solo mix while any channel is
    ///     soloed, then plays whichever is active. Each mix diffs against its own previous
    ///     buffer; turning solo off swaps playback back to the full buffer, which the
    ///     background renders have kept up to date.
    /// </summary>
    private void StartRender(bool startPlayback)
    {
        _rendering = true;
        _modelDirty = false;
        _remixPending = false;
        StatusLabel = "Rendering audio…";
        StatusProgress = 0f;
        StatusDone = 0;
        StatusTotal = 0;

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

    /// <summary>
    ///     Sets <see cref="PendingError" /> unless it repeats the last alerted message, so a
    ///     run of failing re-renders raises one dialog instead of one per edit. A successful
    ///     render or export clears the memory, so the same message alerts again later.
    /// </summary>
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