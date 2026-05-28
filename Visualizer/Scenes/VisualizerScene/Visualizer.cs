using System.Diagnostics;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Audio;
using Shared.Helpers.Miscellaneous;
using Shared.Helpers.Positioning;
using Shared.Objects;
using Shared.Renderer.Settings;
using Sundex.Engine;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Renderer.Enums;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;
using Sundex.Engine.Text;
using Sundex.Engine.Text.Allocationless;
using ThirtyDollarConverter.Objects;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarEncoder.Wave;
using ThirtyDollarParser;
using VisualizerScene.Objects.Playfield;
using VisualizerScene.UI;
using VisualizerScene.Settings;
using Sundex.Components.Abstractions;

namespace VisualizerScene;

[PreloadGraphicsContext]
public class Visualizer : Scene, IGamePreloadable
{
    public const string Version = "2.0.0 (Insider Build)";
    private readonly FpsCounter _fpsCounter = new();

    #region Stopwatches

    private readonly Stopwatch _seekDelayStopwatch;

    #endregion

    private readonly VisualizerSettings _settings;

    private readonly string[] _startingSequences;
    private readonly DollarStoreCamera _tempCamera;
    private readonly DollarStoreCamera _textCamera;
    private readonly CancellationTokenSource _tokenSource = new();
    private readonly ThirtyDollarWorkflow _workflow;
    private PlayerBar? _playerBar;

    private BackingAudio? _backingAudio;
    private StringFormatter? _debugFormatter;
    private GLInfo _glInfo = null!;

    private int _height;

    private ulong _updateId;
    private int _width;
    private CursorType _cursorType;

    /// <summary>
    ///     Creates a TDW sequence visualizer.
    /// </summary>
    /// <param name="game">The Game instance.</param>
    /// <param name="settings">The visualizer settings object this uses.</param>
    /// <param name="workflow">The workflow dependency.</param>
    /// ///
    /// <param name="sequenceLocations">The location of the sequence.</param>
    public Visualizer(Game game, VisualizerSettings settings,
        ThirtyDollarWorkflow workflow, string?[] sequenceLocations) : base(game)
    {
        _workflow = workflow;
        _seekDelayStopwatch = new Stopwatch();

        _width = game.ClientSize.X;
        _height = game.ClientSize.Y;
        _settings = settings;
        _startingSequences = sequenceLocations
            .Where(location => location is not null)
            .Cast<string>()
            .ToArray();

        _tempCamera = new DollarStoreCamera((0, -300f, 0), new Vector2i(_width, _height), settings.ScrollSpeed);
        _textCamera = new DollarStoreCamera((0, 0, 0), new Vector2i(_width, _height), settings.ScrollSpeed);

        _seekDelayStopwatch.Start();

        PlayfieldSizing = new PlayfieldSizing(settings.EventSize)
        {
            SoundMargin = settings.EventMargin,
            SoundsOnASingleLine = settings.LineAmount
        };

        _workflow.HandleAfterSequenceLoad = HandleAfterSequenceLoad;
    }

    public static VisualizerFonts VisualizerFonts { get; private set; } = null!;

    public PlayfieldSizing PlayfieldSizing { get; }

    public PlayfieldContainer PlayfieldContainer { get; private set; } = null!;

    public VisualizerTextContainer TextContainer { get; private set; } = null!;

    private Layout Overlay => TextContainer.Overlay;

    private CancellationToken Token => _tokenSource.Token;
    public float Scale { get; set; } = 1f;
    public string? Greeting { get; set; }

    private SequenceIndices SequenceIndices => _workflow.SequenceIndices;
    private Memory<SequenceInfo> Sequences => _workflow.SequenceInfos;
    private TimedEvents TimedEvents => _workflow.TimedEvents;
    private Placement[] ExtractedSpeedEvents => _workflow.ExtractedSpeedEvents;
    private bool ShowDebugInfo => _workflow.ShowDebugInfo;

    private SequencePlayer SequencePlayer => _workflow.SequencePlayer;

    public static void Preload(AssetProvider assetProvider)
    {
        VisualizerFonts = new VisualizerFonts(assetProvider);
    }

    /// <summary>
    ///     This method loads the sequence, textures and sounds.
    /// </summary>
    /// <exception cref="Exception">Exception thrown when one of the arguments is invalid.</exception>
    public override void Initialize(InitArguments initArguments)
    {
        _glInfo = initArguments.GLInfo;
        TextContainer = new VisualizerTextContainer(VisualizerFonts, _width, _height, Scale)
        {
            Greeting =
            {
                Value = Greeting ??= "DON'T LECTURE ME WITH YOUR THIRTY DOLLAR VISUALIZER",
                FontSize = 36f * Scale
            }
        };

        TextContainer.Greeting.SetPosition((_width / 2f, -200f, 0.25f), PositionAlign.Center);

        var playfieldSettings = new PlayfieldSettings
        {
            SampleHolder = _workflow.SampleHolder ?? throw new Exception("SampleHolder is null"),
            AtlasStore = _workflow.AtlasStore ?? throw new Exception("AtlasStore is null"),
            PlayfieldSizing = PlayfieldSizing,
            RenderScale = Scale,
            Fonts = VisualizerFonts,
            ScrollSpeed = _settings.ScrollSpeed
        };

        PlayfieldContainer =
            new PlayfieldContainer(playfieldSettings, SequencePlayer, new Vector2i(_width, _height));
        PlayfieldContainer.Camera.OnZoom = zoom =>
        {
            UpdateStaticRenderables(_width, _height, zoom);
            SetStatusMessage($"[Camera]: Setting zoom to: {zoom:0.##%}");
        };
        UpdateStaticRenderables(_width, _height, Scale);

        var playerBarContext = new UIContext { Camera = _textCamera, RequestCursor = type => _cursorType = type };
        _playerBar = new PlayerBar(playerBarContext,
            () => SceneManager.TransitionTo("home"),
            () => SequencePlayer.TogglePause(),
            () =>
            {
                PlayfieldContainer.Reset();
                SequencePlayer.Seek(0);
                PlayfieldContainer.ResetAllAnimations();
            },
            progress =>
            {
                if (TimedEvents.Placement.Length == 0) return;
                SequencePlayer.Seek((long)(progress *
                                           SequencePlayer.GetTimeFromIndex(TimedEvents.Placement[^1].Index)));
            }
        );

        PlayfieldContainer.BackgroundPlane.TransitionToColor(new Vector4(0x1a / 255f, 0x1b / 255f, 0x26 / 255f, 1), 0);

        try
        {
            if (_startingSequences.Length < 1) return;
            _workflow.UpdateSequences(_startingSequences).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            SetStatusMessage($"[Sequence Loader] Failed to load sequence with error: \'{e}\'", 10000);
        }
    }

    public override void Resize(int w, int h)
    {
        var resize = new Vector2i(w, h);
        _textCamera.Viewport = resize;
        _textCamera.UpdateMatrix();

        var greeting = TextContainer.Greeting;
        greeting.SetPosition(greeting.Position - (_width - w) / 2f * Vector3.UnitX);

        Overlay.Resize(w, h);
        PlayfieldContainer.Resize(resize);
        _playerBar?.Resize();
        UpdateStaticRenderables(w, h, PlayfieldContainer.Camera.GetRenderScale());

        _width = w;
        _height = h;
    }

    public override void Start()
    {
    }

    public override void Render(RenderArguments args)
    {
        var deltaTime = args.Delta;
        // sets debug values if debugging is enabled.
        RunDebugUpdate(deltaTime);

        // get static values from current camera, for this frame
        _tempCamera.CopyFrom(PlayfieldContainer.Camera);

        PlayfieldContainer.Render((float)deltaTime);

        // render the greeting
        TextContainer.RenderGreeting(_tempCamera);

        // renders the static layout
        TextContainer.RenderStaticText(_textCamera);

        if (_playerBar is null) return;
        _playerBar.UpdateAlpha(Game.MouseState, Game.ClientSize, (float)deltaTime, TimedEvents.Placement.Length == 0);
        _playerBar.RootPanel.Context.Render();
    }

    public override void TransitionedTo()
    {
        TextContainer.Greeting.Value = Greeting ??= "DON'T LECTURE ME WITH YOUR THIRTY DOLLAR VISUALIZER";
        _workflow.HandleAfterSequenceLoad = HandleAfterSequenceLoad;
        // TODO: this is a workaround for now
        Resize(Game.ClientSize.X, Game.ClientSize.Y);
    }

    public override void Update(UpdateArguments updateArgs)
    {
        _cursorType = CursorType.Default;
        _workflow.Update();
        PlayfieldContainer.Update(updateArgs.Delta);

        if (_playerBar is not null && _playerBar.CurrentAlpha >= 0.01f)
        {
            var p_stopwatch = SequencePlayer.GetTimingStopwatch();
            var p_elapsed = p_stopwatch.ElapsedMilliseconds;
            var p_total = TimedEvents.Placement.Length > 0
                ? SequencePlayer.GetTimeFromIndex(TimedEvents.Placement[^1].Index)
                : 0;
            _playerBar.ProgressBar.Progress = p_total > 0 ? (float)p_elapsed / p_total : 0;
            _playerBar.CurrentTimeLabel.Value = TimeString(p_elapsed);
            _playerBar.TotalTimeLabel.Value = TimeString(p_total);
            _playerBar.PlayPauseButton.Label.Value = p_stopwatch.IsRunning ? "Pause" : "Play";

            _playerBar.Update(_playerBar.RootPanel.Context);
        }

        var cursor = _cursorType switch
        {
            CursorType.Default => MouseCursor.Default,
            CursorType.Pointer => MouseCursor.PointingHand,
            CursorType.ResizeX => MouseCursor.ResizeEW,
            CursorType.ResizeY => MouseCursor.ResizeNS,
            _ => MouseCursor.Default
        };

        if (Game.Cursor != cursor)
            Game.Cursor = cursor;

        // checks if there is a backing audio
        if (_backingAudio is null) return;

        // syncs the backing audio to the current sequence time
        var stopwatch = SequencePlayer.GetTimingStopwatch();
        _backingAudio.UpdatePlayState(stopwatch.IsRunning);
        _backingAudio.SyncTime(stopwatch.Elapsed);
    }

    public override void Shutdown()
    {
        SequencePlayer.Die();
        PlayfieldContainer.Dispose();
    }

    public override void FileDrop(string?[] locations)
    {
        switch (locations.Length)
        {
            case < 1:
                return;
            case 1:
                if (locations[0]?.EndsWith(".wav") ?? false)
                {
                    UpdateBackingTrack(locations[0]!);
                    return;
                }

                break;
        }

        FileDrop(locations, true);
    }

    public override void Mouse(MouseState mouseState, KeyboardState keyboardState)
    {
        _playerBar?.MouseEvent(mouseState, Vector2.One);
        // gets scroll
        var scroll = mouseState.ScrollDelta;
        if (scroll == Vector2.Zero) return;

        var new_delta = Vector3.UnitY * (scroll.Y * 100f);

        // if control is pressed handle zoom
        if (keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl))
            PlayfieldContainer.Camera.ZoomStep(scroll.Y);
        // otherwise scrolls the camera
        else
            PlayfieldContainer.Camera.ScrollDelta(new_delta);
    }

    public override void Keyboard(KeyboardState state)
    {
        const int seekLength = 1000;
        var stopwatch = SequencePlayer.GetTimingStopwatch();

        // extract modifier buttons
        var left_control = state.IsKeyDown(Keys.LeftControl);
        var right_control = state.IsKeyDown(Keys.RightControl);
        var left_shift = state.IsKeyDown(Keys.LeftShift);
        var right_shift = state.IsKeyDown(Keys.RightShift);

        // generic modifier checks
        var control = left_control || right_control;
        var shift = left_shift || right_shift;

        // toggle play / pause
        switch (state.IsKeyPressed(Keys.Space))
        {
            case true:
                SequencePlayer.TogglePause();
                if (!shift)
                    SetStatusMessage(stopwatch.IsRunning switch
                    {
                        true => "[Playback]: Resumed",
                        false => "[Playback]: Paused"
                    }, 500);
                break;
        }

        // toggle camera modes
        var oldFollowMode = PlayfieldContainer.CameraFollowMode;
        PlayfieldContainer.CameraFollowMode = state.IsKeyPressed(Keys.C) switch
        {
            true when oldFollowMode is CameraFollowMode.None => CameraFollowMode.CurrentLine,
            true when oldFollowMode is CameraFollowMode.CurrentLine => CameraFollowMode.TDWLike,
            true when oldFollowMode is CameraFollowMode.TDWLike => CameraFollowMode.NoAnimationCurrentLine,
            true when oldFollowMode is CameraFollowMode.NoAnimationCurrentLine => CameraFollowMode
                .NoAnimationTDW,
            true when oldFollowMode is CameraFollowMode.NoAnimationTDW => CameraFollowMode.None,
            _ => oldFollowMode
        };

        // bookmark handlers
        switch (left_control)
        {
            case true when left_shift:
            {
                for (var i = 0; i < 10; i++)
                {
                    var key = (Keys)((int)Keys.D0 + i);
                    if (!state.IsKeyPressed(key)) continue;
                    SequencePlayer.ClearBookmark(i);
                    SetStatusMessage($"[Playback] Cleared Bookmark: {i}");
                }

                break;
            }
            case true:
            {
                for (var i = 0; i < 10; i++)
                {
                    var key = (Keys)((int)Keys.D0 + i);
                    if (!state.IsKeyPressed(key)) continue;
                    var bookmark_time = SequencePlayer.SetBookmark(i);
                    SetStatusMessage($"[Playback] Setting Bookmark {i} To: {TimeString(bookmark_time)}");
                }

                if (state.IsKeyDown(Keys.Equal) && IsSeekTimeoutPassed(5))
                {
                    RestartSeekTimer();
                    PlayfieldContainer.Camera.ZoomStep(+1);
                }

                if (state.IsKeyDown(Keys.Minus) && IsSeekTimeoutPassed(5))
                {
                    RestartSeekTimer();
                    PlayfieldContainer.Camera.ZoomStep(-1);
                }

                if (state.IsKeyPressed(Keys.D))
                {
                    _workflow.ShowDebugInfo = !_workflow.ShowDebugInfo;
                    TextContainer.ShowDebug = _workflow.ShowDebugInfo;
                    SetStatusMessage(_workflow.ShowDebugInfo switch
                    {
                        true => "[Debug]: Enabled",
                        false => "[Debug]: Disabled"
                    });
                }

                break;
            }

            default:
            {
                for (var i = 0; i < 10; i++)
                {
                    var key = (Keys)((int)Keys.D0 + i);
                    if (!state.IsKeyPressed(key)) continue;
                    var time = SequencePlayer.SeekToBookmark(i);
                    SetStatusMessage($"[Playback] Seeking To Bookmark {i}: {TimeString(time)}");
                }

                break;
            }
        }

        // set message if camera mode is updated
        if (oldFollowMode != PlayfieldContainer.CameraFollowMode)
            SetStatusMessage($"[Camera] Follow Mode is now: {PlayfieldContainer.CameraFollowMode}");

        // check backwards seeking
        var elapsed = stopwatch.ElapsedMilliseconds;
        if (state.IsKeyDown(Keys.Left) && IsSeekTimeoutPassed())
        {
            RestartSeekTimer();
            var seek = seekLength;
            if (shift)
            {
                seek /= 10;
                if (control) seek /= 10;
            }

            var change = Math.Max(elapsed - seek, 0);

            SetStatusMessage($"[Playback]: Seeking To: {TimeString(change)}");
            SequencePlayer.Seek(change);
        }

        // check forwards seeking
        if (state.IsKeyDown(Keys.Right) && IsSeekTimeoutPassed())
        {
            RestartSeekTimer();
            var seek = seekLength;
            if (shift)
            {
                seek /= 10;
                if (control) seek /= 10;
            }

            var change = elapsed + seek;

            SetStatusMessage($"[Playback]: Seeking To: {TimeString(change)}");
            SequencePlayer.Seek(change);
        }

        // check volume increase
        if (state.IsKeyDown(Keys.Up) && IsSeekTimeoutPassed(7))
        {
            RestartSeekTimer();
            SequencePlayer.SetVolume(SequencePlayer.Volume + 0.01f);
            SetStatusMessage($"[Playback]: Volume = {SequencePlayer.Volume * 100:0.##}%");
        }

        // check volume decrease
        if (state.IsKeyDown(Keys.Down) && IsSeekTimeoutPassed(7))
        {
            RestartSeekTimer();
            SequencePlayer.SetVolume(Math.Max(0f, SequencePlayer.Volume - 0.01f));
            SetStatusMessage($"[Playback]: Volume = {SequencePlayer.Volume * 100:0.##}%");
        }

        // check previous sequence seeking
        if (state.IsKeyDown(Keys.PageUp) && IsSeekTimeoutPassed() && SequenceIndices.Ends.Length > 0)
        {
            RestartSeekTimer();
            var requested_sequence =
                Math.Clamp(PlayfieldContainer.CurrentSequence - 2, -1, SequenceIndices.Ends.Length - 1);
            if (requested_sequence == -1)
            {
                SequencePlayer.Seek(0);
            }
            else
            {
                var (index, _) = SequenceIndices.Ends[requested_sequence];
                SequencePlayer.Seek(SequencePlayer.GetTimeFromIndex(index));
            }

            SetStatusMessage(
                $"[Playback]: Seeking To Sequence ({requested_sequence + 2} - {SequenceIndices.Ends.Length})");
        }

        // check next sequence seeking
        if (state.IsKeyDown(Keys.PageDown) && IsSeekTimeoutPassed() && SequenceIndices.Ends.Length > 0)
        {
            RestartSeekTimer();
            var requested_sequence =
                Math.Clamp(PlayfieldContainer.CurrentSequence, 0, SequenceIndices.Ends.Length - 1);
            var (index, _) = SequenceIndices.Ends[requested_sequence];

            SequencePlayer.Seek(SequencePlayer.GetTimeFromIndex(index));
            SetStatusMessage(requested_sequence + 1 < SequenceIndices.Ends.Length
                ? $"[Playback]: Seeking To Sequence ({requested_sequence + 2} - {SequenceIndices.Ends.Length})"
                : "[Playback]: Seeking To The End");
        }

        // check for restarting the current sequences
        if (!state.IsKeyPressed(Keys.R)) return;
        PlayfieldContainer.Reset();

        if (control && shift)
        {
            FileDrop(Sequences.ToArray().Select(s => s.FileLocation).Where(File.Exists).ToArray(), true);
            return;
        }

        PlayfieldContainer.Camera.ScrollTo((0, -300, 0));
        PlayfieldContainer.BackgroundPlane.Reset(0.16f);
        SequencePlayer.Seek(0);

        if (shift) stopwatch.Stop();
        PlayfieldContainer.ResetAllAnimations();
    }

    /// <summary>
    ///     Converts milliseconds to a time string.
    /// </summary>
    /// <param name="milliseconds">Milliseconds passed.</param>
    /// <returns>A formatted time string.</returns>
    private static string TimeString(long milliseconds)
    {
        var timespan = TimeSpan.FromMilliseconds(milliseconds);
        var format = "";

        if (timespan.Hours > 0)
            format += @"hh\:";

        format += @"mm\:ss\.ff";

        return timespan.ToString(format);
    }

    public Task HandleAfterSequenceLoad(TimedEvents events, SequencePlayer sequencePlayer)
    {
        TextContainer.ShowControls = false;

        PlayfieldContainer.Camera.ScrollTo((0, -300, 0));

        PlayfieldContainer.BackgroundPlane.Reset(0.66f);
        PlayfieldContainer.ChangeFromTimedEvents(events);

        return Task.CompletedTask;
    }

    private void SetStatusMessage(string message, int hideAfterMs = 2000)
    {
        var update = Overlay.Get<TextSlice>("update");

        if (update.Value == message) return;
        update.Value = message;
        unchecked
        {
            var old_id = ++_updateId;
            Task.Run(async () =>
            {
                if (hideAfterMs < 0) return;
                await Task.Delay(hideAfterMs, Token);
                if (old_id == _updateId)
                    update.Value = string.Empty;
            }, Token);
        }
    }

    private void RunDebugUpdate(double deltaTime)
    {
        if (!ShowDebugInfo) return;
        if (_debugFormatter == null)
        {
            _debugFormatter = new StringFormatter(
                """
                [Debug]
                FPS: {fps:float:0.##}
                Audio Engine: {audioEngine:string:16}
                Resampler: {resampler:string:40}

                Sequence ({currentSequence:int} - {maxSequences:int}): {sequenceLocation:string:256}
                BPM: {bpm:float:0.##:16}
                Time: {elapsedTime:time:16}
                Volume: {volume:float:0.##:16}%

                Current ({currentNoteIndex:int}): {currentNote:string:32}
                Next ({nextNoteIndex:int}): {nextNote:string:32}
                In: {nextBeatMs:float:0.##:16}ms / {beatsToNextBeat:float:0.##:16} beats

                [OpenGL]
                Version: {glInfoVersion:string:64}
                Renderer: {glInfoRenderer:string:256}
                Max Texture Size: {glInfoMaxTexture2DSize:int}
                Max Texture Layers: {glInfoMaxTexture2DLayers:int}

                """);

            _debugFormatter.Set("glInfoVersion", _glInfo.Version);
            _debugFormatter.Set("glInfoRenderer", _glInfo.Renderer);
            _debugFormatter.Set("glInfoMaxTexture2DSize", _glInfo.MaxTexture2DSize);
            _debugFormatter.Set("glInfoMaxTexture2DLayers", _glInfo.MaxTexture2DLayers);
            _debugFormatter.Set("resampler", _workflow.EncoderSettings.Resampler.Name);
        }

        // define values used in generating the debug string.
        var bpm = 300f;
        var elapsed_time = SequencePlayer.GetTimingStopwatch().Elapsed;
        var elapsed_milliseconds = SequencePlayer.GetTimingStopwatch().ElapsedMilliseconds;

        var current_note = "None";
        var current_note_idx = 0;

        var next_note = "None";
        var next_note_idx = 0;
        var next_beat_ms = 0f;
        var beats_to_next_beat = 0f;

        var fps = _fpsCounter.GetAverageFPS(1 / deltaTime);
        var audio_engine = SequencePlayer.AudioContext.Name;

        var volume = PlayfieldContainer.SequenceVolume;
        var soundReferences =
            _workflow.SampleHolder.StringToSoundReferences.GetAlternateLookup<ReadOnlySpan<char>>();

        // remove full path from sequence filename.
        ReadOnlySpan<char> sequence_location = "None";
        if (Sequences.Length > 0 && Sequences.Length > PlayfieldContainer.CurrentSequence)
            sequence_location = Sequences.Span[PlayfieldContainer.CurrentSequence].FileLocation;

        var folder_index = sequence_location.LastIndexOf(Path.DirectorySeparatorChar);
        if (folder_index != -1)
        {
            var temp_idx = folder_index + 1;
            if (temp_idx < sequence_location.Length)
                sequence_location = sequence_location[temp_idx..];
        }

        // get current info.
        var current_index = Math.Max(0, SequencePlayer.PlacementIndex - 1);
        var placement_length = TimedEvents.Placement.Length;

        // get accurate bpm info.
        foreach (var ev in ExtractedSpeedEvents)
        {
            if (ev.Index >= SequencePlayer.GetIndexFromTime(elapsed_milliseconds)) break;
            var val = (float)ev.Event.Value;

            bpm = ev.Event.ValueScale switch
            {
                ValueScale.None => val,
                ValueScale.Add => bpm + val,
                ValueScale.Times => bpm * val,
                ValueScale.Divide => bpm / val,

                _ => bpm
            };
        }

        // get next note info.
        if (current_index < placement_length)
        {
            var normalized_time = SequencePlayer.GetIndexFromTime(elapsed_milliseconds);
            var current_placement = TimedEvents.Placement[current_index];

            current_note_idx = (int)current_placement.SequenceIndex;
            if (current_placement.Event.SoundEvent is not null &&
                soundReferences.TryGetValue(current_placement.Event.SoundEvent, out var sound))
                current_note = sound.Id;

            var current_time = current_placement.Index;

            for (var i = current_index; i < TimedEvents.Placement.Length; i++)
            {
                var i_placement = TimedEvents.Placement[i];
                var i_time = i_placement.Index;
                if (i_time == current_time) continue;

                var is_timing_event = !(i_placement.Audible && i_placement.Event.SoundEvent switch
                {
                    "!cut" => false,
                    "_pause" => false,
                    "!stop" => false,
                    "#icut" => false,
                    _ => true
                });

                if (is_timing_event) continue;
                var soundEvent = i_placement.Event.SoundEvent;
                if (soundEvent is not null)
                {
                    soundReferences.TryGetValue(soundEvent, out sound);
                    next_note = sound?.Id ?? "null";
                }

                next_note_idx = (int)i_placement.SequenceIndex;
                if (i_time > normalized_time) next_beat_ms = (i_time - normalized_time) / 100f;
                beats_to_next_beat = next_beat_ms / 1000f * (bpm / 60f);
                break;
            }
        }

        var debug = Overlay.Get<TextSlice>("debug");
        _debugFormatter.Set("fps", fps);
        _debugFormatter.Set("audioEngine", audio_engine);
        _debugFormatter.Set("currentSequence", PlayfieldContainer.CurrentSequence + 1);
        _debugFormatter.Set("maxSequences", Sequences.Length);
        _debugFormatter.Set("sequenceLocation", sequence_location);
        _debugFormatter.Set("bpm", bpm);
        _debugFormatter.Set("elapsedTime", elapsed_time);
        _debugFormatter.Set("volume", volume);
        _debugFormatter.Set("currentNote", current_note);
        _debugFormatter.Set("currentNoteIndex", current_note_idx);
        _debugFormatter.Set("nextNote", next_note);
        _debugFormatter.Set("nextNoteIndex", next_note_idx);
        _debugFormatter.Set("nextBeatMs", next_beat_ms);
        _debugFormatter.Set("beatsToNextBeat", beats_to_next_beat);

        var newValue = _debugFormatter.Value;
        debug.Value = newValue.Length > 1024 ? newValue[..1024] : newValue;
    }

    private void UpdateStaticRenderables(int w, int h, float scale)
    {
        PlayfieldContainer.StaticCamera.SetRenderScale(scale);

        scale = Math.Min(scale, 1f);
        var width_scale = w / scale - w;
        var height_scale = h / scale - h;

        var backgroundPlane = PlayfieldContainer.BackgroundPlane;
        var flashOverlay = PlayfieldContainer.FlashOverlayPlane;

        var background = backgroundPlane.Scale;
        var b_z = backgroundPlane.Position.Z;
        backgroundPlane.Position = (-width_scale / 2f, -height_scale / 2f, b_z);
        backgroundPlane.Scale = (w + width_scale, h + height_scale, background.Z);

        var flash = flashOverlay.Scale;
        var f_z = flashOverlay.Position.Z;
        flashOverlay.Scale = (w + width_scale, h + height_scale, flash.Z);
        flashOverlay.SetPosition((-width_scale / 2f, -height_scale / 2f, f_z));
    }

    public void UpdateBackingTrack(string? location)
    {
        if (location == null)
        {
            _backingAudio?.Stop();
            _backingAudio = null;
            return;
        }

        var decoder = new WaveDecoder();
        var file_stream = File.OpenRead(location);
        var pcm_data = decoder.Read(file_stream);

        var audio = pcm_data.ReadAsFloat32Array(true);

        if (audio == null) return;
        _backingAudio = new BackingAudio(SequencePlayer.GetContext(), audio, (int)pcm_data.SampleRate);
        _backingAudio.Play();
    }

    private void FileDrop(IReadOnlyCollection<string?> locations, bool resetTime)
    {
        var log = Overlay.Get<TextSlice>("log");
        PlayfieldContainer.Camera.ScrollTo((0, -300, 0));

        if (locations.Count < 1) return;

        Task.Run(async () =>
        {
            log.Value = "Loading...";
            try
            {
                await _workflow.UpdateSequences(locations.ToArray(), resetTime);
            }
            catch (Exception e)
            {
                SetStatusMessage($"[Sequence Loader] Failed to load sequence with error: \'{e}\'", 10000);
            }
            finally
            {
                log.Value = string.Empty;
            }
        }, Token);
    }

    private bool IsSeekTimeoutPassed(int divide = 1)
    {
        const int seekTimeout = 250;
        return _seekDelayStopwatch.ElapsedMilliseconds > seekTimeout / divide;
    }

    private void RestartSeekTimer()
    {
        _seekDelayStopwatch.Restart();
    }
}