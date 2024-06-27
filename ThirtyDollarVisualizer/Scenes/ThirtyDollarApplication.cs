using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.Fonts;
using SixLabors.ImageSharp.PixelFormats;
using ThirtyDollarConverter.Objects;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarEncoder.Wave;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Audio;
using ThirtyDollarVisualizer.Helpers.Color;
using ThirtyDollarVisualizer.Helpers.Decoders;
using ThirtyDollarVisualizer.Helpers.Positioning;
using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Objects.Settings;
using ThirtyDollarVisualizer.Objects.Text;

namespace ThirtyDollarVisualizer.Scenes;

public sealed class ThirtyDollarApplication : ThirtyDollarWorkflow, IScene
{
    private static Texture? MissingTexture;
    private static Texture? ICutTexture;
    private static Vector4 DefaultBackgroundColor => new(0.21f, 0.22f, 0.24f, 1f);
    
    private readonly Stopwatch _file_update_stopwatch = new();

    private readonly CachedDynamicText _log_text = new()
    {
        FontStyle = FontStyle.Bold
    };

    private readonly BasicDynamicText _debug_text = new()
    {
        FontStyle = FontStyle.Bold
    };

    private readonly Stopwatch _open_stopwatch = new();
    private readonly Stopwatch _seek_delay_stopwatch = new();

    private readonly CachedDynamicText _update_text = new()
    {
        FontStyle = FontStyle.Bold
    };

    private readonly DollarStoreCamera Camera;
    private readonly DollarStoreCamera TempCamera;
    private readonly List<Renderable> start_objects = new();
    private readonly List<Renderable> static_objects = new();
    private readonly DollarStoreCamera StaticCamera;
    private readonly List<Renderable> text_objects = new();
    private readonly DollarStoreCamera TextCamera;
    private readonly CancellationTokenSource TokenSource = new();
    private readonly Dictionary<string, Texture> ValueTextCache = new();

    private BackgroundPlane BackgroundPlane = null!;
    private Renderable? _controls_text;
    private SoundRenderable? _drag_n_drop;
    private ColoredPlane _flash_overlay = null!;
    private Renderable? _greeting;
    private bool _reset_time;
    private Dictionary<string, Texture> _texture_cache = null!;

    private ulong _update_id;
    private Renderable? _version_text;
    private ColoredPlane _visible_area = null!;
    private Dictionary<string, Texture> _volume_text_cache = null!;

    private BackingAudio? BackingAudio;
    private int CreationLeftMargin;
    private long CurrentResizeFrame;

    // This is currently a hack, but I can't think of any other way to fix this without restructuring the code.
    private int DividerCount;
    private int Height;
    
    // This is a hack because I am lazy
    private bool is_first_time_scale = true;

    // These are needed for some events, because I don't want to pollute the placement events. They're polluted enough as they are.
    private float LastBPM = 300f;
    private ulong LastDividerIndex;
    private int LeftMargin;
    private Manager Manager = null!;
    private int PlayfieldWidth;

    private Memory<Memory<SoundRenderable?>> TDW_images = Array.Empty<Memory<SoundRenderable?>>();
    private int CurrentSequence;

    private int Width;

    /// <summary>
    ///     Creates a TDW sequence visualizer.
    /// </summary>
    /// <param name="width">The width of the visualizer.</param>
    /// <param name="height">The height of the visualizer.</param>
    /// <param name="sequence_locations">The location of the sequence.</param>
    /// <param name="audio_context">The audio context the application will use.</param>
    public ThirtyDollarApplication(int width, int height, IEnumerable<string?> sequence_locations,
        AudioContext? audio_context = null) : base(audio_context)
    {
        Width = width;
        Height = height;
        Sequences = GetSequenceInfos(sequence_locations);

        TempCamera = new DollarStoreCamera((0, -300f, 0), new Vector2i(Width, Height));
        Camera = new DollarStoreCamera((0, -300f, 0), new Vector2i(Width, Height));
        StaticCamera = new DollarStoreCamera((0, 0, 0), new Vector2i(Width, Height));
        TextCamera = new DollarStoreCamera((0, 0, 0), new Vector2i(Width, Height));

        _open_stopwatch.Start();
        _seek_delay_stopwatch.Start();
        _file_update_stopwatch.Start();

        _reset_time = true;
    }

    private CancellationToken Token => TokenSource.Token;
    public int RenderableSize { get; set; } = 64;
    public int MarginBetweenRenderables { get; set; } = 12;
    public int ElementsOnSingleLine { get; init; } = 16;
    public CameraFollowMode CameraFollowMode { get; set; } = CameraFollowMode.TDW_Like;
    public string? BackgroundVertexShaderLocation { get; init; }
    public string? BackgroundFragmentShaderLocation { get; init; }
    public float Zoom { get; set; } = 1f;
    public float Scale { get; set; } = 1f;
    public string? Greeting { get; set; }
    private double SequenceVolume { get; set; }

    /// <summary>
    ///     This method loads the sequence, textures and sounds.
    /// </summary>
    /// <exception cref="Exception">Exception thrown when one of the arguments is invalid.</exception>
    public void Init(Manager manager)
    {
        GetSampleHolder().GetAwaiter();
        if (_reset_time)
            SequencePlayer.Stop().GetAwaiter().GetResult();

        if (is_first_time_scale)
        {
            RenderableSize = (int)(RenderableSize * Scale);
            MarginBetweenRenderables = (int)(MarginBetweenRenderables * Scale);
            _debug_text.SetFontSize(14f * Scale);
            is_first_time_scale = false;
        }

        static_objects.Clear();
        start_objects.Clear();

        Manager = manager;
        MissingTexture ??= new Texture("ThirtyDollarVisualizer.Assets.Textures.action_missing.png");
        ICutTexture ??= new Texture("ThirtyDollarVisualizer.Assets.Textures.action_icut.png");

        Log("Loaded sequence and placement.");

        Shader? optional_shader = null;
        if (BackgroundVertexShaderLocation is not null && BackgroundFragmentShaderLocation is not null)
            optional_shader = new Shader(BackgroundVertexShaderLocation, BackgroundFragmentShaderLocation);

        BackgroundPlane = new BackgroundPlane(DefaultBackgroundColor, new Vector3(-Width, -Height, 1f),
            new Vector3(Width * 2, Height * 2, 0),
            optional_shader);

        _flash_overlay = new ColoredPlane(new Vector4(1f, 1f, 1f, 0f), new Vector3(-Width, -Height, 0.75f),
            new Vector3(Width * 2, Height * 2, 0));

        static_objects.Add(BackgroundPlane);
        static_objects.Add(_flash_overlay);

        PlayfieldWidth = ElementsOnSingleLine * (RenderableSize + MarginBetweenRenderables) + MarginBetweenRenderables +
                         15 /*px Padding in the site. */;

        LeftMargin = (int)((float)Width / 2 - (float)PlayfieldWidth / 2);

        _visible_area = new ColoredPlane(new Vector4(0, 0, 0, 0.25f), new Vector3(LeftMargin, -Height, 0.5f),
            new Vector3(PlayfieldWidth, Height * 2, 0));
        static_objects.Add(_visible_area);

        var font_family = Fonts.GetFontFamily();
        var greeting_font = font_family.CreateFont(36 * Scale, FontStyle.Bold);

        _greeting ??= new CachedDynamicText
        {
            FontStyle = FontStyle.Bold,
            FontSizePx = 36f * Scale,
            Value = Greeting ?? "DON'T LECTURE ME WITH YOUR THIRTY DOLLAR VISUALIZER"
        }.WithPosition((Width / 2f, -200f, 0.25f), PositionAlign.Center);

        _controls_text ??= new StaticText
        {
            FontStyle = FontStyle.Bold,
            FontSizePx = 14f * Scale,
            Value = """
                    All controls:

                    Scroll -> Scroll up / down.
                    Ctrl+Scroll -> Change the zoom.
                    Up / Down -> Control the application's volume.
                    Left / Right -> Seek the sequence.
                    R -> Reload the current sequence.
                    C -> Change the camera modes.
                    F -> Toggle between fullscreen and windowed.
                    Space -> Pause / resume the sequence.
                    Escape -> Close the program.
                    0-9 -> Seek to bookmark.
                    Ctrl+0-9 -> Set bookmark to current time.
                    Ctrl+Shift+0-9 -> Clear given bookmark time.
                    Ctrl+D -> Show debugging info.
                    Page Up/Down -> Seek to previous/next sequence.

                    """
        }.WithPosition((10, 0f, 0));

        const string version_string = "1.1.4";
        var text = new StaticText
        {
            FontStyle = FontStyle.Bold,
            FontSizePx = 14f * Scale,
            Value = $"""
                     Don't use the audio generated by this
                     visualizer for a video, as it isn't
                     accurate at the moment. Use the
                     Thirty Dollar GUI instead.

                     Check regularly for updates at:
                     https://github.com/t1stm/ThirtyDollarTools

                     Current Version: {version_string}
                     """
        };

        _version_text ??= text.WithPosition((10, Height, 0), PositionAlign.BottomLeft);

        text_objects.Add(_controls_text);
        text_objects.Add(_version_text);

        _log_text.SetPosition((20, 20, 0));
        _log_text.SetFontSize(48f * Scale);
        _log_text.FontStyle = FontStyle.Bold;
        
        _debug_text.SetPosition((10,30,0));

        text_objects.Add(_log_text);
        text_objects.Add(_update_text);
        _update_text.SetPosition((10, 10, 0));

        UpdateStaticRenderables(Width, Height, Zoom);

        Log = str => SetStatusMessage(str);

        if (_drag_n_drop == null)
        {
            var dnd_texture = new Texture(greeting_font, "Drop files on the window to start.");
            _drag_n_drop = new SoundRenderable(dnd_texture,
                new Vector3(Width / 2f - dnd_texture.Width / 2f, 0, 0.25f),
                new Vector2(dnd_texture.Width, dnd_texture.Height));

            start_objects.Add(_drag_n_drop);
            _drag_n_drop.UpdateModel(false);
        }
        
        if (Sequences.Length < 1) return;

        try
        {
            UpdateSequences(Sequences.ToArray().Select(s => s.FileLocation).ToArray()).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            SetStatusMessage($"[Sequence Loader] Failed to load sequence with error: \'{e}\'", 10000);
            return;
        }

        Manager.CheckErrors();
    }

    public void Resize(int w, int h)
    {
        var resize = new Vector2i(w, h);

        TextCamera.Viewport = StaticCamera.Viewport = Camera.Viewport = resize;
        GL.Viewport(0, 0, w, h);

        Camera.UpdateMatrix();
        StaticCamera.UpdateMatrix();
        TextCamera.UpdateMatrix();

        var visible_position = UpdateStaticRenderables(w, h, Zoom);

        var current_margin = visible_position.X;

        foreach (var r in start_objects)
        {
            var pos = r.GetPosition();
            var delta_x = (Width - w) / 2f;

            pos -= delta_x * Vector3.UnitX;
            r.SetPosition(pos);
        }

        _greeting?.SetPosition(_greeting.GetPosition() - (Width - w) / 2f * Vector3.UnitX);
        _controls_text?.SetPosition((10, 150, 0));
        _version_text?.SetPosition((10, h - 10f, 0), PositionAlign.BottomLeft);

        Width = w;
        Height = h;

        LeftMargin = (int)((float)Width / 2 - (float)PlayfieldWidth / 2);

        var current_update = CurrentResizeFrame = _open_stopwatch.ElapsedMilliseconds;

        Task.Run(() =>
        {
            if (TDW_images.Length < 1) return;
            for (var i = 0; i < TDW_images.Length; i++)
            {
                var sequence = i;
                foreach (var image in TDW_images.Span[sequence].Span)
                {
                    if (image == null) continue;
                    if (CurrentResizeFrame != current_update) break;

                    var original_offset = image.GetTranslation();
                    var new_offset = new Vector3(original_offset)
                    {
                        X = current_margin - CreationLeftMargin
                    };

                    image.SetTranslation(new_offset);
                }
            }
        }, Token);
    }

    public void Start()
    {
        if (Sequences.Length < 1) return;
        SequencePlayer.Stop().GetAwaiter().GetResult();
        SequencePlayer.Start().GetAwaiter().GetResult();
    }

    public void Render()
    {
        Manager.CheckErrors();

        // get static values from current camera, for this frame
        var camera = TempCamera;
        camera.CopyFrom(Camera);
        
        // render all static objects
        foreach (var renderable in static_objects) renderable.Render(StaticCamera);

        // render the greeting
        _greeting?.Render(camera);
        
        // set render culling limits
        var clamped_scale = Math.Min(Zoom, 1f);
        var width_scale = Width / clamped_scale - Width;
        var height_scale = Height / clamped_scale - Height;

        // get render culling camera values
        var camera_x = camera.Position.X - width_scale;
        var camera_y = camera.Position.Y;
        var camera_xw = camera_x + Width + width_scale;
        var camera_yh = camera_y + Height;

        // get render culling object values
        var size_renderable = RenderableSize + MarginBetweenRenderables;
        var repeats_renderable = PlayfieldWidth / size_renderable;

        // account for padding between objects
        var padding_rows = repeats_renderable * 2;
        var dividers_size = size_renderable * DividerCount;
        
        // fix values when the zoom is changed
        camera_y -= height_scale;
        camera_yh += height_scale;

        // render objects if any
        if (TDW_images.Length > 0)
        {
            // gets the current sequence
            var tdw_span = TDW_images.Span[CurrentSequence].Span;

            // finds the index of the object at the start of the view
            var start = (int)Math.Max(0, repeats_renderable * (camera_y / size_renderable) - dividers_size - padding_rows);

            // same but for the end of the view
            var end = (int)Math.Min(tdw_span.Length,
                repeats_renderable * (camera_y / size_renderable) +
                repeats_renderable * ((Height + height_scale) / size_renderable / clamped_scale) +
                padding_rows);

            // renders all objects in the start - end range
            for (var i = start; i < end; i++)
            {
                var renderable = tdw_span[i];
                RenderRenderable(renderable);
            }
        }

        // renders all start objects, when visible
        foreach (var renderable in start_objects) RenderRenderable(renderable);

        // renders every text object
        foreach (var renderable in text_objects) renderable.Render(TextCamera);
        if (Debug) _debug_text.Render(TextCamera);

        return;

        // inline method that is called in the renderer
        void RenderRenderable(Renderable? renderable)
        {
            Manager.CheckErrors();
            if (renderable is null) return;

            var position = renderable.GetPosition();
            var translation = renderable.GetTranslation();

            var scale = renderable.GetScale();
            var place = position + translation;

            // Bounds checks for viewport.

            if (place.X + scale.X < camera_x || place.X - scale.X > camera_xw) return;
            if (place.Y + scale.Y < camera_y || place.Y - scale.Y > camera_yh) return;

            renderable.Render(camera);
        }
    }

    /// <summary>
    /// Call this method when there is a change to the objects of a given sequence.
    /// </summary>
    /// <param name="sequence_index">The changed sequence's index.</param>
    public void HandleSequenceChange(int sequence_index)
    {
        if (TimedEvents.Placement.Length <= 1) return;
        var old_sequence = CurrentSequence;
        CurrentSequence = sequence_index;
            
        if (old_sequence != CurrentSequence)
        {
            // reset debugging values to their default ones
            SequenceVolume = 100d;
            LastBPM = 300;
        }

        if (old_sequence >= CurrentSequence) return;
        Camera.SetPosition((0,-300,0));
        
        // values for the loop below
        var current_placement = SequencePlayer.PlacementIndex;
        var current_index = TimedEvents.Placement[current_placement].Index;
        var max_index_change = TimedEvents.TimingSampleRate / 2; // 500 ms
        var max_index = current_index + (ulong)max_index_change;
        
        // checks if there is a color event in the next 500ms
        for (var i = current_placement; i < TimedEvents.Placement.Length; i++)
        {
            var index = Math.Clamp(i, 0, TimedEvents.Placement.Length - 1);
            var placement = TimedEvents.Placement[index];
            if (placement.Index > max_index) break;
            if (placement.Event.SoundEvent is "!bg") return;
        }

        // changes the background color to the default one if there isn't a color event in the next 500ms
        BackgroundPlane.TransitionToColor(DefaultBackgroundColor, 0.33f);
    }

    public void Update()
    {
        // check if one of the sequences has been updated, and handle it
        if (_file_update_stopwatch.ElapsedMilliseconds > 250) HandleIfSequenceUpdate();

        var last_frame_time = (float)Manager.UpdateTime;
        
        // spawns the camera update thread
        Camera.Update(last_frame_time);
        
        // updates the background's transitions
        BackgroundPlane.Update();

        // sets debug values if debugging is enabled.
        if (Debug)
        {
            RunDebugUpdate();
        }

        // checks if there is a backing audio
        if (BackingAudio is null) return;

        // syncs the backing audio to the current sequence time
        var stopwatch = SequencePlayer.GetTimingStopwatch();
        BackingAudio.UpdatePlayState(stopwatch.IsRunning);
        BackingAudio.SyncTime(stopwatch.Elapsed);
    }

    public void Close()
    {
        SequencePlayer.Die();
        Camera.Die();
    }

    public void FileDrop(string?[] locations)
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

    public void Mouse(MouseState mouse_state, KeyboardState keyboard_state)
    {
        // gets scroll
        var scroll = mouse_state.ScrollDelta;
        if (scroll == Vector2.Zero) return;

        var new_delta = Vector3.UnitY * (scroll.Y * 100f);

        // if control is pressed handle zoom
        if (keyboard_state.IsKeyDown(Keys.LeftControl) || keyboard_state.IsKeyDown(Keys.RightControl))
        {
            HandleZoomControl(scroll.Y);
        }
        // otherwise scrolls the camera
        else
        {
            Camera.ScrollDelta(new_delta);
        }
    }

    /// <summary>
    /// Converts milliseconds to a time string.
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
    
    public async void Keyboard(KeyboardState state)
    {
        const int seek_length = 1000;
        var stopwatch = SequencePlayer.GetTimingStopwatch();

        // toggle play / pause
        switch (state.IsKeyPressed(Keys.Space))
        {
            case true:
                SequencePlayer.TogglePause();
                SetStatusMessage(SequencePlayer.GetTimingStopwatch().IsRunning switch
                {
                    true => "[Playback]: Resumed",
                    false => "[Playback]: Paused"
                });
                break;
        }

        // toggle camera modes
        var old_camera = CameraFollowMode;
        CameraFollowMode = state.IsKeyPressed(Keys.C) switch
        {
            true when CameraFollowMode is CameraFollowMode.None => CameraFollowMode.Current_Line,
            true when CameraFollowMode is CameraFollowMode.Current_Line => CameraFollowMode.TDW_Like,
            true when CameraFollowMode is CameraFollowMode.TDW_Like => CameraFollowMode.No_Animation_Current_Line,
            true when CameraFollowMode is CameraFollowMode.No_Animation_Current_Line => CameraFollowMode.No_Animation_TDW,
            true when CameraFollowMode is CameraFollowMode.No_Animation_TDW => CameraFollowMode.None,
            _ => CameraFollowMode
        };

        // toggle fullscreen
        if (state.IsKeyPressed(Keys.F))
        {
            Manager.ToggleFullscreen();
        }

        // extract modifier buttons
        var left_control = state.IsKeyDown(Keys.LeftControl);
        var right_control = state.IsKeyDown(Keys.RightControl);
        var left_shift = state.IsKeyDown(Keys.LeftShift);
        var right_shift = state.IsKeyDown(Keys.RightShift);
        
        // generic modifier checks
        var control = left_control || right_control;
        var shift = left_shift || right_shift;
        
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
                    HandleZoomControl(+1);
                }

                if (state.IsKeyDown(Keys.Minus) && IsSeekTimeoutPassed(5))
                {
                    RestartSeekTimer();
                    HandleZoomControl(-1);
                }
            
                if (state.IsKeyPressed(Keys.D))
                {
                    Debug = !Debug;
                    SetStatusMessage(Debug switch
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
                    var time = await SequencePlayer.SeekToBookmark(i);
                    SetStatusMessage($"[Playback] Seeking To Bookmark {i}: {TimeString(time)}");
                }

                break;
            }
        }

        // set message if camera mode is updated
        if (old_camera != CameraFollowMode) SetStatusMessage($"[Camera] Follow Mode is now: {CameraFollowMode}");

        // check backwards seeking
        var elapsed = stopwatch.ElapsedMilliseconds;
        if (state.IsKeyDown(Keys.Left) && IsSeekTimeoutPassed())
        {
            RestartSeekTimer();
            var seek = seek_length;
            if (shift)
            {
                seek /= 10;
                if (control)
                {
                    seek /= 10;
                }
            }
            var change = Math.Max(elapsed - seek, 0);

            SetStatusMessage($"[Playback]: Seeking To: {TimeString(change)}");
            await SequencePlayer.Seek(change);
        }

        // check forwards seeking
        if (state.IsKeyDown(Keys.Right) && IsSeekTimeoutPassed())
        {
            RestartSeekTimer();
            var seek = seek_length;
            if (shift)
            {
                seek /= 10;
                if (control)
                {
                    seek /= 10;
                }
            }
            var change = elapsed + seek;

            SetStatusMessage($"[Playback]: Seeking To: {TimeString(change)}");
            await SequencePlayer.Seek(change);
        }

        // check volume increase
        if (state.IsKeyDown(Keys.Up) && IsSeekTimeoutPassed(7))
        {
            RestartSeekTimer();
            SequencePlayer.AudioContext.GlobalVolume += 0.01f;
            SetStatusMessage($"[Playback]: Global Volume = {SequencePlayer.AudioContext.GlobalVolume * 100:0.##}%");
        }

        // check volume decrease
        if (state.IsKeyDown(Keys.Down) && IsSeekTimeoutPassed(7))
        {
            RestartSeekTimer();
            SequencePlayer.AudioContext.GlobalVolume = Math.Max(0f, SequencePlayer.AudioContext.GlobalVolume - 0.01f);
            SetStatusMessage($"[Playback]: Global Volume = {SequencePlayer.AudioContext.GlobalVolume * 100:0.##}%");
        }

        // check previous sequence seeking
        if (state.IsKeyDown(Keys.PageUp) && IsSeekTimeoutPassed() && SequenceIndices.Ends.Length > 0)
        {
            RestartSeekTimer();
            var requested_sequence = Math.Clamp(CurrentSequence - 2, -1, SequenceIndices.Ends.Length - 1);
            if (requested_sequence == -1)
            {
                await SequencePlayer.Seek(0);
            }
            else
            {
                var (index, _) = SequenceIndices.Ends[requested_sequence];
                await SequencePlayer.Seek(SequencePlayer.GetTimeFromIndex(index));
            }
            SetStatusMessage($"[Playback]: Seeking To Sequence ({requested_sequence + 2} - {SequenceIndices.Ends.Length})");
        }
        
        // check next sequence seeking
        if (state.IsKeyDown(Keys.PageDown) && IsSeekTimeoutPassed() && SequenceIndices.Ends.Length > 0)
        {
            RestartSeekTimer();
            var requested_sequence = Math.Clamp(CurrentSequence, 0, SequenceIndices.Ends.Length - 1);
            var (index, _) = SequenceIndices.Ends[requested_sequence];

            await SequencePlayer.Seek(SequencePlayer.GetTimeFromIndex(index));
            SetStatusMessage(requested_sequence + 1 < SequenceIndices.Ends.Length
                ? $"[Playback]: Seeking To Sequence ({requested_sequence + 2} - {SequenceIndices.Ends.Length})"
                : "[Playback]: Seeking To The End");
        } 

        // check for restarting the current sequences
        if (!state.IsKeyPressed(Keys.R)) return;
        FileDrop(Sequences.ToArray().Select(s => s.FileLocation).Where(File.Exists).ToArray(), true);
    }

    protected override void HandleAfterSequenceLoad(TimedEvents events)
    {
        CurrentSequence = 0;
        foreach (var renderable in start_objects) renderable.IsVisible = false;

        if (_controls_text != null && _version_text != null)
        {
            _controls_text.IsVisible = false;
            _version_text.IsVisible = false;
        }

        Camera.ScrollTo((0, -300, 0));
        BackgroundPlane.TransitionToColor(DefaultBackgroundColor, 0.66f);
        DividerCount = 0;

        var sequences_count = events.Sequences.Length;
        var tdw_images = new SoundRenderable?[sequences_count][];
        for (var index = 0; index < tdw_images.Length; index++)
        {
            var sequence = events.Sequences[index];
            tdw_images[index] = new SoundRenderable?[sequence.Events.Length];
        }

        var font_family = Fonts.GetFontFamily();

        CreationLeftMargin = LeftMargin;
        var wh = new Vector2i(RenderableSize, RenderableSize);

        _texture_cache = new Dictionary<string, Texture>();
        _volume_text_cache = new Dictionary<string, Texture>();

        var value_font = font_family.CreateFont(RenderableSize / 3.625f, FontStyle.Bold);

        // funny number 👍
        var volume_font = font_family.CreateFont(RenderableSize * 0.22f, FontStyle.Bold);
        var volume_color = new Rgba32(204, 204, 204, 1f);

        Task.Run(() =>
        {
            foreach (var placement in events.Placement.Where(p =>
                         p.Event is { SoundEvent: "!divider", Value: > 0 and <= 9 }
                             or BookmarkEvent { Value: >= 0 and <= 9 }))
            {
                if (Token.IsCancellationRequested) return;
                var time = placement.Index * 1000f / events.TimingSampleRate;
                var idx = (int)placement.Event.Value;

                SequencePlayer.SetBookmarkTo(idx, (long)time);
            }
        }, Token);

        try
        {
            for (var index = 0; index < events.Sequences.Length; index++)
            {
                var flex_box = new FlexBox(new Vector2i(LeftMargin + 7, 0),
                    new Vector2i(PlayfieldWidth + MarginBetweenRenderables, Height), MarginBetweenRenderables);
                
                var sequence = events.Sequences[index];
                for (var i = 0; i < sequence.Events.Length; i++)
                {
                    var ev = sequence.Events[i];
                    if (string.IsNullOrEmpty(ev.SoundEvent) ||
                        (ev.SoundEvent.StartsWith('#') && ev is not ICustomActionEvent) || ev is IHiddenEvent)
                        continue;

                    CreateEventRenderable(tdw_images[index], i, ev, _texture_cache, wh, flex_box,
                        ValueTextCache, _volume_text_cache, value_font,
                        volume_color, volume_font);
                }

                var decreasing_events = sequence.Events.Where(r => r.SoundEvent is "!stop" or "!loopmany");

                foreach (var e in decreasing_events)
                {
                    var textures = e.Value;
                    for (var val = textures; val >= 0; val--)
                    {
                        var search = val.ToString("0.##");
                        if (ValueTextCache.ContainsKey(search)) continue;

                        var texture = new Texture(value_font, search);
                        ValueTextCache.Add(search, texture);
                    }
                }
            }

            if (!ValueTextCache.ContainsKey("0"))
            {
                var texture = new Texture(value_font, "0");
                ValueTextCache.Add("0", texture);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        Manager.RenderBlock.Wait(Token);
        TDW_images = tdw_images.Select(i => i.AsMemory()).ToArray();
        Manager.RenderBlock.Release();
        SequenceVolume = 100;
    }

    protected override void SetSequencePlayerSubscriptions(SequencePlayer player)
    {
        player.SubscribeSequenceChange(HandleSequenceChange);
        player.SubscribeActionToEvent(string.Empty, NormalSubscription);
        player.SubscribeActionToEvent("!speed", SpeedEventHandler);
        player.SubscribeActionToEvent("!bg", BackgroundEventHandler);
        player.SubscribeActionToEvent("!flash", FlashEventHandler);
        player.SubscribeActionToEvent("!pulse", PulseEventHandler);
        player.SubscribeActionToEvent("!loopmany", LoopManyEventHandler);
        player.SubscribeActionToEvent("!stop", StopEventHandler);
        player.SubscribeActionToEvent("!divider", DividerEventHandler);
        player.SubscribeActionToEvent("!volume", VolumeEventHandler);
    }


    private void SetStatusMessage(string message, int hide_after_ms = 2000)
    {
        if (_update_text.Value == message) return;
        _update_text.SetTextContents(message);
        unchecked
        {
            var old_id = ++_update_id;
            Task.Run(async () =>
            {
                if (hide_after_ms < 0) return;
                await Task.Delay(hide_after_ms, Token);
                if (old_id == _update_id)
                    _update_text.SetTextContents(string.Empty);
            }, Token);
        }
    }

    /// <summary>
    ///     Creates a Thirty Dollar Website renderable with the texture of the event and its value and volume as children.
    /// </summary>
    private void CreateEventRenderable(SoundRenderable?[] tdw_images, int index, BaseEvent ev,
        IDictionary<string, Texture> texture_cache, Vector2i wh,
        FlexBox flex_box,
        IDictionary<string, Texture> value_text_cache, IDictionary<string, Texture> volume_text_cache, Font font,
        Rgba32 volume_color, Font volume_font)
    {
        var dll_location = SampleHolder!.DownloadLocation;
        var image = $"{dll_location}/Images/" + ev.SoundEvent?.Replace("!", "action_") + ".png";

        if (!File.Exists(image))
            switch (ev)
            {
                case IndividualCutEvent:
                    if (ICutTexture == null)
                        throw new Exception("Texture for individual cutting elements isn't loaded.");
                    texture_cache.TryAdd(image, ICutTexture);
                    break;

                default:
                    if (MissingTexture == null) throw new Exception("Texture for missing elements isn't loaded.");
                    Log($"Asset: \'{image}\' not found.");
                    texture_cache.TryAdd(image, MissingTexture);
                    break;
            }

        texture_cache.TryGetValue(image, out var texture);
        if (texture == null)
        {
            texture = new Texture(image);
            texture_cache.Add(image, texture);
        }

        var width_height = new Vector2i(wh.X, wh.Y);
        var aspect_ratio = (float)texture.Width / texture.Height;

        switch (aspect_ratio)
        {
            case > 1:
                width_height.Y = (int)(width_height.Y / aspect_ratio);
                break;
            case < 1:
                width_height.X = (int)(width_height.X * aspect_ratio);
                break;
        }

        var box_position = flex_box.AddBox(wh);
        box_position.Z = -0.5f;
        var plane_position = new Vector3(box_position);

        switch (aspect_ratio)
        {
            case > 1:
            {
                float marginY = wh.Y - width_height.Y;
                plane_position.Y += marginY / 2;
                break;
            }
            case < 1:
            {
                float marginX = wh.X - width_height.X;
                plane_position.X += marginX / 2;
                break;
            }
        }

        var plane = new SoundRenderable(texture, plane_position, width_height);

        #region Value Text

        var value = ev.Value.ToString("0.##");
        if (ev.Value > 0 && !(ev.SoundEvent!.StartsWith('!') || ev.SoundEvent!.StartsWith('_'))) value = "+" + value;

        value = ev.ValueScale switch
        {
            ValueScale.Add => "+" + value,
            ValueScale.Times => "×" + value,
            ValueScale.Divide => "/" + value,
            _ => value
        };

        var polluted_value_texture = false;
        Texture? value_texture = null;

        switch (ev)
        {
            case IndividualCutEvent ice:
            {
                polluted_value_texture = true;
                var cut_sounds = ice.CutSounds.ToArray();
                var available_textures =
                    cut_sounds.Where(r => File.Exists($"{dll_location}/Images/{r}.png"));

                var textures = available_textures.Select(t => new Texture($"{dll_location}/Images/{t}.png")).ToArray();
                value_texture = new Texture(textures, 2, Scale);

                break;
            }

            case { SoundEvent: "!bg" }:
            {
                var parsed_value = (long)ev.Value;
                var seconds = (parsed_value >> 24) / 1000f;
                value = seconds.ToString("0.##");

                var r = (byte)parsed_value;
                var g = (byte)(parsed_value >> 8);
                var b = (byte)(parsed_value >> 16);

                value_texture = new Texture(font, new Rgb24(r, g, b), value);
                break;
            }

            case { SoundEvent: "!volume" }:
            {
                if (ev.ValueScale is not ValueScale.Times and not ValueScale.Divide)
                    value += "%";
                break;
            }

            case { SoundEvent: "!pulse" }:
            {
                var parsed_value = (long)ev.Value;
                var repeats = (byte)parsed_value;
                var pulse_times = (short)(parsed_value >> 8);

                value = $"{repeats}, {pulse_times}";
                break;
            }
        }

        if (ev.OriginalLoop % 1f != 0)
            for (var i = ev.OriginalLoop - 1; i >= 0; i--)
            {
                var text = i.ToString("0.##");
                value_text_cache.TryGetValue(text, out var new_value_texture);
                if (new_value_texture != null) continue;

                new_value_texture = new Texture(font, text, volume_color);
                if (!polluted_value_texture)
                    value_text_cache.Add(text, new_value_texture);
            }

        if (texture == MissingTexture)
        {
            value = $"{ev.SoundEvent}@{value}";
            polluted_value_texture = true;
        }

        if ((value_texture == null && (ev.Value != 0 || polluted_value_texture) && ev.SoundEvent is not "_pause") ||
            ev.SoundEvent is "!transpose")
        {
            value_text_cache.TryGetValue(value, out value_texture);
            if (value_texture == null)
            {
                value_texture = new Texture(font, value, volume_color);
                if (!polluted_value_texture)
                    value_text_cache.Add(value, value_texture);
            }
        }

        if (value_texture is not null)
        {
            var text_position = new Vector3
            {
                X = plane_position.X + width_height.X / 2f,
                Y = box_position.Y + RenderableSize,
                Z = box_position.Z - 0.1f
            };

            var text = new TexturedPlane(value_texture, Vector3.Zero, (value_texture.Width, value_texture.Height));
            text.SetPosition(text_position, PositionAlign.Center);

            plane.SetValueRenderable(text);
            plane.Children.Add(text);
        }

        #endregion

        #region Volume Text

        if (ev.Volume is not null and not 100d)
        {
            var volume = ev.Volume ?? throw new Exception("Invalid volume check.");
            var volume_text = volume.ToString("0.#") + "%";

            volume_text_cache.TryGetValue(volume_text, out var volume_texture);
            if (volume_texture == null)
            {
                volume_texture = new Texture(volume_font, volume_text);
                volume_text_cache.Add(volume_text, volume_texture);
            }

            var text_position = new Vector3
            {
                X = box_position.X + RenderableSize + 6,
                Y = box_position.Y,
                Z = box_position.Z
            };
            text_position.Z -= 0.5f;

            var text = new TexturedPlane(volume_texture, Vector3.Zero,
                (volume_texture.Width, volume_texture.Height));

            text.SetPosition(text_position, PositionAlign.TopRight);
            plane.Children.Add(text);
        }

        #endregion

        #region Pan Text

        var pan = 0f;
        if (ev is PannedEvent panned_event) pan = panned_event.Pan;

        if (pan != 0f)
        {
            var pan_text = Math.Abs(pan).ToString(".#");

            switch (pan)
            {
                case < 0:
                    pan_text += "|";
                    break;

                case > 0:
                    pan_text = "|" + pan_text;
                    break;
            }

            volume_text_cache.TryGetValue(pan_text, out var pan_texture);

            if (pan_texture == null)
            {
                pan_texture = new Texture(volume_font, pan_text);
                volume_text_cache.Add(pan_text, pan_texture);
            }

            var text_position = new Vector3
            {
                X = box_position.X,
                Y = box_position.Y,
                Z = box_position.Z
            };
            text_position.Z -= 0.5f;

            var text = new TexturedPlane(pan_texture, text_position,
                (pan_texture.Width, pan_texture.Height));
            plane.Children.Add(text);
        }

        #endregion

        tdw_images[index] = plane;
        plane.UpdateModel(false);

        if (ev.SoundEvent is not "!divider") return;

        flex_box.NewLine();
        flex_box.NewLine();
        DividerCount++;
    }

    private SoundRenderable? GetRenderable(Placement placement, int sequence_index)
    {
        if (sequence_index >= TDW_images.Length) return null;
        
        var len = TDW_images.Span[sequence_index].Length;
        var placement_idx = (int)placement.SequenceIndex;
        var element = placement_idx >= len || placement_idx < 0 ? null : TDW_images.Span[sequence_index].Span[placement_idx];

        return element;
    }

    private void CameraBoundsCheck(Placement placement, int sequence_index)
    {
        var element = GetRenderable(placement, sequence_index);
        if (element == null) return;

        var position = element.GetPosition() + element.GetTranslation();
        var scale = element.GetScale();
        
        switch (CameraFollowMode)
        {
            case var follow_mode when follow_mode.HasFlag(CameraFollowMode.TDW_Like):
            {
                float margin = RenderableSize;

                if (!Camera.IsOutsideOfCameraView(position, scale, margin) &&
                    placement.Event.SoundEvent is not "!divider") break;

                var pos = new Vector3(0, position.Y - margin, 0f);
                
                if (CameraFollowMode.HasFlag(CameraFollowMode.No_Animation))
                {
                    Camera.SetPosition(pos);
                    return;
                }
                Camera.ScrollTo(pos);
                return;
            }

            case var follow_mode when follow_mode.HasFlag(CameraFollowMode.Current_Line):
            {
                var pos = position * Vector3.UnitY - Vector3.UnitY * (Height / 2f);

                if (CameraFollowMode.HasFlag(CameraFollowMode.No_Animation))
                {
                    Camera.SetPosition(pos);
                    return;
                }
                Camera.ScrollTo(pos);
                return;
            }
        }
    }

    private void NormalSubscription(Placement placement, int index)
    {
        var element = GetRenderable(placement, index);
        if (element == null) return;
        CameraBoundsCheck(placement, index);

        if ((placement.Event.SoundEvent?.StartsWith('!') ?? false) || placement.Event is ICustomActionEvent)
        {
            element.Fade();
            element.Expand();
        }
        else if (placement.Event is not ICustomActionEvent)
        {
            element.Bounce();
        }
    }

    private void SpeedEventHandler(Placement placement, int index)
    {
        var val = (float)placement.Event.Value;

        LastBPM = placement.Event.ValueScale switch
        {
            ValueScale.None => val,
            ValueScale.Add => LastBPM + val,
            ValueScale.Times => LastBPM * val,
            ValueScale.Divide => LastBPM / val,

            _ => LastBPM
        };
    }

    private void BackgroundEventHandler(Placement placement, int index)
    {
        var (color, seconds) = BackgroundParser.ParseFromDouble(placement.Event.Value);
        BackgroundPlane.TransitionToColor(color, seconds);
    }

    private void FlashEventHandler(Placement placement, int index)
    {
        Task.Run(async () =>
        {
            await ColorTools.ChangeColor(_flash_overlay, new Vector4(1, 1, 1, 1), 0.125f);
            await ColorTools.ChangeColor(_flash_overlay, new Vector4(0, 0, 0, 0), 0.25f);
        }, Token);
    }

    private void PulseEventHandler(Placement placement, int index)
    {
        var parsed_value = (long)placement.Event.Value;
        var repeats = (byte)parsed_value;
        float frequency = (short)(parsed_value >> 8);

        Camera.Pulse(repeats, frequency * 1000f / (LastBPM / 60));
        StaticCamera.Pulse(repeats, frequency * 1000f / (LastBPM / 60));
    }

    private void LoopManyEventHandler(Placement placement, int index)
    {
        var element = GetRenderable(placement, index);
        element?.SetValue(placement.Event, ValueTextCache, ValueChangeWrapMode.RemoveTexture);
    }

    private void StopEventHandler(Placement placement, int index)
    {
        var element = GetRenderable(placement, index);
        element?.SetValue(placement.Event, ValueTextCache, ValueChangeWrapMode.ResetToDefault);
    }

    private void DividerEventHandler(Placement placement, int index)
    {
        LastDividerIndex = placement.SequenceIndex;
    }
    
    private void VolumeEventHandler(Placement placement, int index)
    {
        SequenceVolume = placement.Event.WorkingVolume;
    }

    private void RunDebugUpdate()
    {
        // define values used in generating the debug string.
        var bpm = 300f;
        var elapsed_milliseconds = SequencePlayer.GetTimingStopwatch().ElapsedMilliseconds;
        var current_note = "None";
        var current_note_idx = 0;
        
        var next_note = "None";
        var next_note_idx = 0;
        var next_beat_ms = 0f;
        var beats_to_next_beat = 0f;
        
        var fps = 1 / Manager.UpdateTime;
        var volume = SequenceVolume;
        
        // remove full path from sequence filename.
        var sequence_location = "None";
        if (Sequences.Length > 0 && Sequences.Length > CurrentSequence)
        {
            sequence_location = Sequences.Span[CurrentSequence].FileLocation;
        }
        
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
            var val = (float) ev.Event.Value;

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
            current_note = current_placement.Event.SoundEvent ?? current_note;
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
                
                next_note = i_placement.Event.SoundEvent;
                next_note_idx = (int)i_placement.SequenceIndex;
                if (i_time > normalized_time) next_beat_ms = (i_time - normalized_time) / 100f;
                beats_to_next_beat = next_beat_ms / 1000f * (bpm / 60f);
                break;
            }
        }
        
        // generate debug string.
        _debug_text.SetTextContents(
            $"""
             [Debug]
             FPS: {fps:0.##}

             Sequence ({CurrentSequence + 1} - {Sequences.Length}): {sequence_location}
             BPM: {bpm}
             Time: {TimeString(elapsed_milliseconds)}
             Volume: {volume:0.##}%

             Current ({current_note_idx}): {current_note}
             Next ({next_note_idx}): {next_note}
             In: {next_beat_ms:0.##ms} / {beats_to_next_beat:0.##} beats
             """);
    }

    private Vector3 UpdateStaticRenderables(int w, int h, float scale)
    {
        Camera.SetRenderScale(scale);
        StaticCamera.SetRenderScale(scale);
        scale = Math.Min(scale, 1f);
        var width_scale = Width / scale - Width;
        var height_scale = Height / scale - Height;

        var background = BackgroundPlane.GetScale();
        BackgroundPlane.SetScale((w + width_scale, h + height_scale, background.Z));
        BackgroundPlane.SetPosition((-width_scale / 2f, -height_scale / 2f, 0));

        var flash = _flash_overlay.GetScale();
        _flash_overlay.SetScale((w + width_scale, h + height_scale, flash.Z));
        _flash_overlay.SetPosition((-width_scale / 2f, -height_scale / 2f, 0));

        var visible = _visible_area.GetScale();
        _visible_area.SetScale((visible.X, h + height_scale * 2, visible.Z));

        var visible_position = _visible_area.GetPosition();
        visible_position.X = w / 2f - visible.X / 2;
        visible_position.Y = -height_scale;

        _visible_area.SetPosition(visible_position);
        return visible_position;
    }

    private void UpdateBackingTrack(string location)
    {
        var decoder = new WaveDecoder();
        var file_stream = File.OpenRead(location);
        var pcm_data = decoder.Read(file_stream);

        var audio = pcm_data.ReadAsFloat32Array(true);

        if (audio == null) return;
        BackingAudio = new BackingAudio(SequencePlayer.GetContext(), audio, (int)pcm_data.SampleRate);
        BackingAudio.Play();
    }

    private void FileDrop(IReadOnlyCollection<string?> locations, bool reset_time)
    {
        _reset_time = reset_time;
        Camera.ScrollTo((0, -300, 0));
        
        if (locations.Count < 1) return;

        Task.Run(async () =>
        {
            _log_text.SetTextContents("Loading...");
            try
            {
                await UpdateSequences(locations.Where(File.Exists).ToArray(), reset_time);
            }
            catch (Exception e)
            {
                SetStatusMessage($"[Sequence Loader] Failed to load sequence with error: \'{e}\'", 10000);
            }
            finally
            {
                _log_text.SetTextContents(string.Empty);
            }
        }, Token);
    }

    private void HandleZoomControl(float scale)
    {
        const float stepping = .05f;
        var camera_scale = Camera.GetRenderScale();
        Zoom = Math.Max(camera_scale + scale * stepping, stepping);
        UpdateStaticRenderables(Width, Height, Zoom);
        SetStatusMessage($"[Camera]: Setting zoom to: {Zoom:0.##%}");
    }

    private bool IsSeekTimeoutPassed(int divide = 1)
    {
        const int seek_timeout = 250;
        return _seek_delay_stopwatch.ElapsedMilliseconds > seek_timeout / divide;
    }

    private void RestartSeekTimer()
    {
        _seek_delay_stopwatch.Restart();
    }
}