using System.Diagnostics;
using Shared;
using Shared.Audio;
using Shared.Objects;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarEncoder.Resamplers;
using ThirtyDollarParser;

namespace EditorScene;

/// <summary>
///     Phase-2 playback. Every arrangement channel renders as its own sequence — the
///     library anchors them all on the project timeline origin, so the per-channel
///     buffers are sample-aligned — and the audible channels are summed into the
///     player buffer. Mute/solo is therefore a remix of cached renders, never a
///     re-render. Model edits re-render the channels incrementally after a debounce,
///     without restarting the player (the incremental path updates audio under the
///     playhead).
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

    private readonly PlacementCalculator _calculator;
    private readonly Dictionary<int, RenderedSequence> _channelRenders = [];
    private readonly Stopwatch _sinceEdit = new();
    private readonly EditorState _state;
    private readonly ThirtyDollarWorkflow _workflow;

    private readonly SampleProcessor _previewProcessor;
    private readonly uint _previewSampleRate;
    private AudibleBuffer? _preview;

    private bool _modelDirty;
    private bool _remixPending;
    private bool _rendering;
    private TimedEvents? _timedEvents;

    public EditorPlayback(ThirtyDollarWorkflow workflow, EditorState state)
    {
        _workflow = workflow;
        _state = state;
        _calculator = new PlacementCalculator(new EncoderSettings
        {
            SampleRate = workflow.EncoderSettings.SampleRate,
            AddVisualEvents = true
        });
        Encoder = new PcmEncoder(workflow.SampleHolder, workflow.EncoderSettings);

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

    /// <summary>True once a buffer was rendered and uploaded — the transport is live.</summary>
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
        if (_rendering) return;
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
        if (_rendering) return;
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
        var player = _workflow.SequencePlayer;
        if (player.GetTimingStopwatch().IsRunning) player.TogglePause();
        player.Seek(0);
    }

    /// <summary>
    ///     Plays one pitched sound (a note being placed or moved), replacing any
    ///     still-playing preview. Suppressed during playback unless
    ///     <see cref="PreviewDuringPlayback" /> is set.
    /// </summary>
    public void PreviewNote(string sound, double value)
    {
        if (IsPlaying && !PreviewDuringPlayback) return;

        StopPreview();
        var audio = _previewProcessor.ProcessEvent(new NormalEvent { SoundEvent = sound, Value = value });
        if (audio.GetLength() == 0) return; // unknown sound or samples still downloading

        _preview = _workflow.SequencePlayer.AudioContext.GetBufferObject(audio, (int)_previewSampleRate);
        _preview.Play();
    }

    /// <summary>
    ///     Renders the merged project fresh and writes it as a WAV. Independent of the
    ///     playback session and of mute/solo — the export always contains every track.
    /// </summary>
    public Task ExportWav(string path)
    {
        // Snapshot on the update thread, render in the background (StartRender's rule).
        var merged = _state.Project.ToSequence();
        return Task.Run(async () =>
        {
            try
            {
                var rendered = await Encoder.GetSequenceAudio(merged);
                Encoder.WriteAsWavFile(path, rendered.Audio);
            }
            catch (Exception e)
            {
                _workflow.Logger.Error("[Editor Playback] WAV export failed: {Exception}", e);
            }
        });
    }

    public void StopPreview()
    {
        if (_preview == null) return;
        _preview.Stop();
        _preview.Delete();
        _preview = null;
    }

    /// <summary>Call once per frame on the update thread.</summary>
    public void Update()
    {
        if (_rendering || _timedEvents == null) return;

        if (_modelDirty && _sinceEdit.ElapsedMilliseconds >= DebounceMs) StartRender(false);
        else if (_remixPending) StartRemix();
    }

    private void StartRender(bool startPlayback)
    {
        _rendering = true;
        _modelDirty = false;
        _remixPending = false;

        // Sequences are snapshotted here, on the update thread — the only thread that
        // mutates the model — so the background render works on a consistent state.
        var project = _state.Project;
        var channels = project.Placements.Select(p => p.Channel).Distinct().ToArray();
        var channel_sequences = channels.ToDictionary(c => c, c => project.ChannelSequence(c));
        var merged = project.ToSequence();
        var audible = channels.Where(_state.IsChannelAudible).ToArray();

        Task.Run(async () =>
        {
            try
            {
                foreach (var (channel, sequence) in channel_sequences)
                    _channelRenders[channel] = _channelRenders.TryGetValue(channel, out var old)
                        ? await Encoder.ComputeIncrementalAudio(old, [sequence])
                        : await Encoder.GetSequenceAudio(sequence);

                foreach (var gone in _channelRenders.Keys.Except(channels).ToArray())
                    _channelRenders.Remove(gone);

                var placement = _calculator.CalculateMany([merged]).ToArray();
                var events = new TimedEvents
                {
                    Sequences = [merged],
                    Placement = placement,
                    TimingSampleRate = (int)_workflow.EncoderSettings.SampleRate
                };

                Upload(events, Mix(audible), startPlayback);
            }
            catch (Exception e)
            {
                _workflow.Logger.Error("[Editor Playback] Render failed: {Exception}", e);
            }
            finally
            {
                _rendering = false;
            }
        });
    }

    private void StartRemix()
    {
        _rendering = true;
        _remixPending = false;

        var audible = _channelRenders.Keys.Where(_state.IsChannelAudible).ToArray();
        var events = _timedEvents!;

        Task.Run(() =>
        {
            try
            {
                Upload(events, Mix(audible), false);
            }
            catch (Exception e)
            {
                _workflow.Logger.Error("[Editor Playback] Remix failed: {Exception}", e);
            }
            finally
            {
                _rendering = false;
            }
        });
    }

    /// <summary>
    ///     Sums the audible channels' aligned buffers. The buffer always spans the
    ///     longest render (even when muted) so the playhead position survives a remix.
    /// </summary>
    private AudioData<float> Mix(int[] audible)
    {
        var channels = _workflow.EncoderSettings.Channels;
        var length = _channelRenders.Count > 0
            ? _channelRenders.Values.Max(r => r.Audio.GetLength())
            : 1;

        var mixer = new AudioMixer(AudioData<float>.WithLength(channels, length));
        foreach (var channel in audible)
            mixer.Sum(new AudioMixer(_channelRenders[channel].Audio));

        return mixer.GetDefault();
    }

    private void Upload(TimedEvents events, AudioData<float> audio, bool startPlayback)
    {
        var player = _workflow.SequencePlayer;
        if (startPlayback) player.Stop();

        var rendered = new RenderedSequence
        {
            TimedEvents = events,
            Audio = audio,
            AudioSampleRate = _workflow.EncoderSettings.SampleRate
        };

        player.UpdateSequence(events, ThirtyDollarWorkflow.GenerateSequenceIndexes(events.Placement),
            rendered, startPlayback);
        _timedEvents = events;

        if (startPlayback) player.Start(_workflow.Game.ThreadRunner);
        else player.AlignToTime();
    }
}
