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
using ThirtyDollarVisualizer.Helpers.Positioning;
using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Objects.Settings;
using ThirtyDollarVisualizer.Objects.Text;

namespace ThirtyDollarVisualizer.Scenes;

public class ThirtyDollarApplication : ThirtyDollarWorkflow, IScene
{
    private const int TimingSampleRate = 100_000;
    private static Texture? MissingTexture;
    private static Texture? ICutTexture;
    private readonly Stopwatch _file_update_stopwatch = new();

    private readonly DynamicText _log_text = new()
    {
        FontStyle = FontStyle.Bold
    };

    private readonly Stopwatch _open_stopwatch = new();
    private readonly Stopwatch _seek_delay_stopwatch = new();

    private readonly DynamicText _update_text = new()
    {
        FontStyle = FontStyle.Bold
    };

    private readonly DollarStoreCamera Camera;
    protected readonly List<Renderable> start_objects = new();
    protected readonly List<Renderable> static_objects = new();
    private readonly DollarStoreCamera StaticCamera;
    protected readonly List<Renderable> text_objects = new();
    private readonly DollarStoreCamera TextCamera;
    private readonly CancellationTokenSource TokenSource = new();
    private readonly Dictionary<string, Texture> ValueTextCache = new();

    private ColoredPlane _background = null!;
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

    // These are needed for some events, because I don't want to pollute the placement events. They're polluted enough as they are.
    private float LastBPM = 300f;
    private ulong LastDividerIndex;
    private int LeftMargin;
    private Manager Manager = null!;
    private int PlayfieldWidth;
    protected Memory<SoundRenderable?> TDW_images;

    private int Width;

    /// <summary>
    ///     Creates a TDW sequence visualizer.
    /// </summary>
    /// <param name="width">The width of the visualizer.</param>
    /// <param name="height">The height of the visualizer.</param>
    /// <param name="sequenceLocation">The location of the sequence.</param>
    /// <param name="audio_context">The audio context the application will use.</param>
    public ThirtyDollarApplication(int width, int height, string? sequenceLocation,
        AudioContext? audio_context = null) : base(audio_context)
    {
        Width = width;
        Height = height;
        _sequence_location = sequenceLocation;

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

    /// <summary>
    ///     This method loads the sequence, textures and sounds.
    /// </summary>
    /// <exception cref="Exception">Exception thrown when one of the arguments is invalid.</exception>
    public virtual void Init(Manager manager)
    {
        GetSampleHolder().GetAwaiter();
        if (_reset_time)
            SequencePlayer.Stop().GetAwaiter().GetResult();

        static_objects.Clear();
        start_objects.Clear();

        Manager = manager;
        MissingTexture ??= new Texture("ThirtyDollarVisualizer.Assets.Textures.action_missing.png");
        ICutTexture ??= new Texture("ThirtyDollarVisualizer.Assets.Textures.action_icut.png");

        Log("Loaded sequence and placement.");

        Shader? optional_shader = null;
        if (BackgroundVertexShaderLocation is not null && BackgroundFragmentShaderLocation is not null)
            optional_shader = new Shader(BackgroundVertexShaderLocation, BackgroundFragmentShaderLocation);

        _background = new ColoredPlane(new Vector4(0.21f, 0.22f, 0.24f, 1f), new Vector3(-Width, -Height, 1f),
            new Vector3(Width * 2, Height * 2, 0),
            optional_shader);

        _flash_overlay = new ColoredPlane(new Vector4(1f, 1f, 1f, 0f), new Vector3(-Width, -Height, 0.75f),
            new Vector3(Width * 2, Height * 2, 0));

        static_objects.Add(_background);
        static_objects.Add(_flash_overlay);

        PlayfieldWidth = ElementsOnSingleLine * (RenderableSize + MarginBetweenRenderables) + MarginBetweenRenderables +
                         15 /*px Padding in the site. */;

        LeftMargin = (int)((float)Width / 2 - (float)PlayfieldWidth / 2);

        _visible_area = new ColoredPlane(new Vector4(0, 0, 0, 0.25f), new Vector3(LeftMargin, -Height, 0.5f),
            new Vector3(PlayfieldWidth, Height * 2, 0));
        static_objects.Add(_visible_area);

        var font_family = Fonts.GetFontFamily();
        var greeting_font = font_family.CreateFont(36, FontStyle.Bold);

        _greeting ??= new DynamicText
        {
            FontStyle = FontStyle.Bold,
            FontSizePx = 36f,
            Value = Greeting ?? "DON'T LECTURE ME WITH YOUR THIRTY DOLLAR VISUALIZER"
        }.WithPosition((Width / 2f, -200f, 0.25f), PositionAlign.Center);

        _controls_text ??= new StaticText
        {
            FontStyle = FontStyle.Bold,
            Value = """
                    All controls:

                    Scroll -> Scroll up / down.
                    Ctrl+Scroll -> Change the zoom.
                    Up / Down -> Control the application's volume.
                    Left / Right -> Seek the sequence.
                    R -> Reload the current sequence.
                    C -> Change the camera modes.
                    Space -> Pause / resume the sequence.
                    Escape -> Close the program.
                    0-9 -> Seek to bookmark.
                    Ctrl+0-9 -> Set bookmark to current time.
                    Ctrl+Shift+0-9 -> Clear given bookmark time.

                    """
        }.WithPosition((10, 0f, 0));

        const string version_string = "1.0.0";
        var text = new StaticText
        {
            FontStyle = FontStyle.Bold,
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
        _log_text.SetFontSize(48f);
        _log_text.FontStyle = FontStyle.Bold;

        text_objects.Add(_log_text);
        text_objects.Add(_update_text);
        _update_text.SetPosition((10, 10, 0));

        UpdateStaticRenderables(Width, Height, Zoom);

        Log = str => SetStatusMessage(str);

        if (_drag_n_drop == null)
        {
            var dnd_texture = new Texture(greeting_font, "Drop a file on the window to start.");
            _drag_n_drop = new SoundRenderable(dnd_texture,
                new Vector3(Width / 2f - dnd_texture.Width / 2f, 0, 0.25f),
                new Vector2(dnd_texture.Width, dnd_texture.Height));

            start_objects.Add(_drag_n_drop);
            _drag_n_drop.UpdateModel(false);
        }

        if (_sequence_location == null) return;

        try
        {
            UpdateSequence(_sequence_location).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return;
        }

        Manager.CheckErrors();
    }

    public virtual void Resize(int w, int h)
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
            foreach (var image in TDW_images.Span)
            {
                if (image is null) continue;

                if (CurrentResizeFrame != current_update) break;

                var original_offset = image.GetTranslation();
                var new_offset = new Vector3(original_offset)
                {
                    X = current_margin - CreationLeftMargin
                };

                image.SetTranslation(new_offset);
            }
        }, Token);
    }

    public void Start()
    {
        if (_sequence_location == null) return;
        SequencePlayer.Stop().GetAwaiter().GetResult();
        SequencePlayer.Start().GetAwaiter().GetResult();
    }

    public virtual void Render()
    {
        Manager.CheckErrors();

        var clamped_scale = Math.Min(Zoom, 1f);
        var width_scale = Width / clamped_scale - Width;
        var height_scale = Height / clamped_scale - Height;

        var camera_x = Camera.Position.X - width_scale;
        var camera_y = Camera.Position.Y;
        var camera_xw = camera_x + Width + width_scale;
        var camera_yh = camera_y + Height;

        camera_y -= height_scale;
        camera_yh += height_scale;

        foreach (var renderable in static_objects) renderable.Render(StaticCamera);

        _greeting?.Render(Camera);

        var size_renderable = RenderableSize + MarginBetweenRenderables;
        var repeats_renderable = PlayfieldWidth / size_renderable;

        var padding_rows = repeats_renderable * 2;
        var dividers_size = size_renderable * DividerCount;
        var tdw_span = TDW_images.Span;

        var start = (int)Math.Max(0, repeats_renderable * (camera_y / size_renderable) - dividers_size - padding_rows);

        var end = (int)Math.Min(TDW_images.Length,
            repeats_renderable * (camera_y / size_renderable) +
            repeats_renderable * ((Height + height_scale) / size_renderable / clamped_scale) +
            padding_rows);

        for (var i = start; i < end; i++)
        {
            var renderable = tdw_span[i];
            if (renderable is null) continue;
            RenderRenderable(renderable);
        }

        foreach (var renderable in start_objects) RenderRenderable(renderable);

        foreach (var renderable in text_objects) renderable.Render(TextCamera);

        return;

        void RenderRenderable(Renderable renderable)
        {
            Manager.CheckErrors();

            var position = renderable.GetPosition();
            var translation = renderable.GetTranslation();

            var scale = renderable.GetScale();
            var place = position + translation;

            // Bounds checks for viewport.

            if (place.X + scale.X < camera_x || place.X - scale.X > camera_xw) return;
            if (place.Y + scale.Y < camera_y || place.Y - scale.Y > camera_yh) return;

            renderable.Render(Camera);
        }
    }

    public virtual void Update()
    {
        if (_file_update_stopwatch.ElapsedMilliseconds > 250) HandleIfSequenceUpdate();

        Camera.Update();

        var stopwatch = SequencePlayer.GetTimingStopwatch();
        if (BackingAudio is not null)
        {
            BackingAudio.UpdatePlayState(stopwatch.IsRunning);
            BackingAudio.SyncTime(stopwatch.Elapsed);
        }
    }

    public void Close()
    {
        SequencePlayer.Stop().GetAwaiter().GetResult();
    }

    public void FileDrop(string? location)
    {
        if (location?.EndsWith(".wav") ?? false)
        {
            UpdateBackingTrack(location);
            return;
        }

        FileDrop(location, true);
    }

    public virtual void Mouse(MouseState mouse_state, KeyboardState keyboard_state)
    {
        var scroll = mouse_state.ScrollDelta;
        if (scroll == Vector2.Zero) return;

        var new_delta = Vector3.UnitY * (scroll.Y * 100f);

        if (keyboard_state.IsKeyDown(Keys.LeftControl))
        {
            HandleZoomControl(scroll.Y);
        }
        else
        {
            Camera.ScrollDelta(new_delta);
            Camera.Update();
        }
    }

    public virtual async void Keyboard(KeyboardState state)
    {
        const int seek_length = 1000;
        var stopwatch = SequencePlayer.GetTimingStopwatch();

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

        var old_camera = CameraFollowMode;
        CameraFollowMode = state.IsKeyPressed(Keys.C) switch
        {
            true when CameraFollowMode is CameraFollowMode.Current_Line => CameraFollowMode.TDW_Like,
            true when CameraFollowMode is CameraFollowMode.TDW_Like => CameraFollowMode.None,
            true when CameraFollowMode is CameraFollowMode.None => CameraFollowMode.Current_Line,
            _ => CameraFollowMode
        };

        if (state.IsKeyDown(Keys.LeftShift) && state.IsKeyDown(Keys.LeftControl))
        {
            for (var i = 0; i < 10; i++)
            {
                var key = (Keys)((int)Keys.D0 + i);
                if (!state.IsKeyPressed(key)) continue;
                SequencePlayer.ClearBookmark(i);
                SetStatusMessage($"[Playback] Cleared Bookmark: {i}");
            }
        }
        else if (state.IsKeyDown(Keys.LeftControl))
        {
            for (var i = 0; i < 10; i++)
            {
                var key = (Keys)((int)Keys.D0 + i);
                if (!state.IsKeyPressed(key)) continue;
                var bookmark_time = SequencePlayer.SetBookmark(i);
                SetStatusMessage($"[Playback] Setting Bookmark {i} To: {bookmark_time}ms");
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
        }
        else
        {
            for (var i = 0; i < 10; i++)
            {
                var key = (Keys)((int)Keys.D0 + i);
                if (!state.IsKeyPressed(key)) continue;
                var time = await SequencePlayer.SeekToBookmark(i);
                SetStatusMessage($"[Playback] Seeking To Bookmark {i}: {time}ms");
            }
        }

        if (old_camera != CameraFollowMode) SetStatusMessage($"[Camera] Follow Mode is now: {CameraFollowMode}");

        var elapsed = stopwatch.ElapsedMilliseconds;
        if (state.IsKeyDown(Keys.Left) && IsSeekTimeoutPassed())
        {
            RestartSeekTimer();
            var change = elapsed - seek_length;

            var (placement, i) = TimedEvents.Placement.Select((placement, i) => (placement, i))
                .MinBy(stack => Math.Abs((long)stack.placement.Index * 1000 / TimingSampleRate - change));
            var placement_index = placement?.Index ?? 0;

            var ms = (long)placement_index * 1000 / TimingSampleRate;
            SetStatusMessage($"[Playback]: Seeking To: {ms}ms");
            await SequencePlayer.Seek(ms);
        }

        if (state.IsKeyDown(Keys.Right) && IsSeekTimeoutPassed())
        {
            RestartSeekTimer();
            var change = elapsed + seek_length;

            var (placement, i) = TimedEvents.Placement.Select((placement, i) => (placement, i))
                .MinBy(stack => Math.Abs((long)stack.placement.Index * 1000 / TimingSampleRate - change));
            var placement_index = placement?.Index ?? 0;

            var ms = (long)placement_index * 1000 / TimingSampleRate;
            SetStatusMessage($"[Playback]: Seeking To: {ms}ms");
            await SequencePlayer.Seek(ms);
        }

        if (state.IsKeyDown(Keys.Up) && IsSeekTimeoutPassed(7))
        {
            RestartSeekTimer();
            SequencePlayer.AudioContext.GlobalVolume += 0.01f;
            SetStatusMessage($"[Playback]: Global Volume = {SequencePlayer.AudioContext.GlobalVolume * 100:0.##}%");
        }

        if (state.IsKeyDown(Keys.Down) && IsSeekTimeoutPassed(7))
        {
            RestartSeekTimer();
            SequencePlayer.AudioContext.GlobalVolume = Math.Max(0f, SequencePlayer.AudioContext.GlobalVolume - 0.01f);
            SetStatusMessage($"[Playback]: Global Volume = {SequencePlayer.AudioContext.GlobalVolume * 100:0.##}%");
        }

        if (!state.IsKeyPressed(Keys.R)) return;
        FileDrop(_sequence_location, true);
    }

    protected override void HandleAfterSequenceLoad(TimedEvents events)
    {
        foreach (var renderable in start_objects) renderable.IsVisible = false;

        if (_controls_text != null && _version_text != null)
        {
            _controls_text.IsVisible = false;
            _version_text.IsVisible = false;
        }

        Camera.ScrollTo((0, -300, 0));
        ColorTools.ChangeColor(_background, new Vector4(0.21f, 0.22f, 0.24f, 1f), 0.66f).GetAwaiter();
        DividerCount = 0;

        var tdw_images = new SoundRenderable?[events.Sequence.Events.Length];
        var font_family = Fonts.GetFontFamily();

        CreationLeftMargin = LeftMargin;
        var flex_box = new FlexBox(new Vector2i(LeftMargin + 7, 0),
            new Vector2i(PlayfieldWidth + MarginBetweenRenderables, Height), MarginBetweenRenderables);
        var wh = new Vector2i(RenderableSize, RenderableSize);

        _texture_cache = new Dictionary<string, Texture>();
        _volume_text_cache = new Dictionary<string, Texture>();
        
        var font = font_family.CreateFont(RenderableSize / 4f, FontStyle.Bold);

        // funny number 👍
        var volume_font = font_family.CreateFont(RenderableSize * 0.203125f, FontStyle.Bold);
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
            for (var i = 0; i < events.Sequence.Events.Length; i++)
            {
                var ev = events.Sequence.Events[i];
                if (string.IsNullOrEmpty(ev.SoundEvent) ||
                    (ev.SoundEvent.StartsWith('#') && ev is not ICustomActionEvent) || ev is IHiddenEvent)
                    continue;

                CreateEventRenderable(tdw_images, i, ev, _texture_cache, wh, flex_box,
                    ValueTextCache, _volume_text_cache, font,
                    volume_color, volume_font);
            }

            var max_decreasing_event = events.Sequence.Events.Where(r => r.SoundEvent is "!stop" or "!loopmany")
                .MaxBy(r => r.Value);

            var textures = max_decreasing_event?.Value ?? 0;

            for (var val = (int)textures - 1; val >= 0; val--)
            {
                var search = val.ToString();
                if (ValueTextCache.ContainsKey(search)) continue;

                var texture = new Texture(font, search);
                ValueTextCache.Add(search, texture);
            }

            if (!ValueTextCache.ContainsKey("0"))
            {
                var texture = new Texture(font, "0");
                ValueTextCache.Add("0", texture);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        Manager.RenderBlock.Wait(Token);
        TDW_images = tdw_images;
        Manager.RenderBlock.Release();
    }

    protected override void SetSequencePlayerSubscriptions(SequencePlayer player)
    {
        player.SubscribeActionToEvent(string.Empty, NormalSubscription);
        player.SubscribeActionToEvent("!speed", SpeedEventHandler);
        player.SubscribeActionToEvent("!bg", BackgroundEventHandler);
        player.SubscribeActionToEvent("!flash", FlashEventHandler);
        player.SubscribeActionToEvent("!pulse", PulseEventHandler);
        player.SubscribeActionToEvent("!loopmany", LoopManyEventHandler);
        player.SubscribeActionToEvent("!stop", StopEventHandler);
        player.SubscribeActionToEvent("!divider", DividerEventHandler);
    }

    protected void SetStatusMessage(string message, int hide_after_ms = 2000)
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
    protected virtual void CreateEventRenderable(SoundRenderable?[] tdw_images, int index, BaseEvent ev,
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
                value_texture = new Texture(textures, 2);

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
                Y = box_position.Y + RenderableSize - MarginBetweenRenderables + 1,
                Z = box_position.Z - 0.1f
            };

            var text = new TexturedPlane(value_texture, Vector3.Zero, (value_texture.Width, value_texture.Height));
            text.SetPosition(text_position, PositionAlign.TopCenter);

            plane.SetValueRenderable(text);
            plane.Children.Add(text);
        }

        #endregion

        #region Volume Text

        if (ev.Volume is not null and not 100d)
        {
            var volume = ev.Volume ?? throw new Exception("Invalid volume check.");
            var volume_text = volume.ToString("0") + "%";

            volume_text_cache.TryGetValue(volume_text, out var volume_texture);
            if (volume_texture == null)
            {
                volume_texture = new Texture(volume_font, volume_text);
                volume_text_cache.Add(volume_text, volume_texture);
            }

            var text_position = new Vector3
            {
                X = box_position.X + RenderableSize - volume_texture.Width,
                Y = box_position.Y,
                Z = box_position.Z
            };
            text_position.Z -= 0.5f;

            var text = new TexturedPlane(volume_texture, text_position,
                (volume_texture.Width, volume_texture.Height));
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

    private SoundRenderable? GetRenderable(Placement placement)
    {
        var len = TDW_images.Length;
        var placement_idx = (int)placement.SequenceIndex;
        var element = placement_idx >= len || placement_idx < 0 ? null : TDW_images.Span[placement_idx];

        return element;
    }

    private void CameraBoundsCheck(Placement placement)
    {
        var element = GetRenderable(placement);
        if (element == null) return;

        var position = element.GetPosition() + element.GetTranslation();
        var scale = element.GetScale();

        switch (CameraFollowMode)
        {
            case CameraFollowMode.TDW_Like:
            {
                float margin = RenderableSize;

                if (!Camera.IsOutsideOfCameraView(position, scale, margin) &&
                    placement.Event.SoundEvent is not "!divider") break;

                Camera.ScrollTo(new Vector3(0, position.Y - margin, 0f));
                break;
            }

            case CameraFollowMode.Current_Line:
            {
                Camera.ScrollTo(position * Vector3.UnitY - Vector3.UnitY * (Height / 2f));
                break;
            }
        }
    }

    private void NormalSubscription(Placement placement, int index)
    {
        var element = GetRenderable(placement);
        if (element == null) return;
        CameraBoundsCheck(placement);

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

            _ => LastBPM
        };
    }

    private void BackgroundEventHandler(Placement placement, int index)
    {
        var parsed_value = (long)placement.Event.Value;

        var r = (byte)parsed_value;
        var g = (byte)(parsed_value >> 8);
        var b = (byte)(parsed_value >> 16);
        var color = new Vector4(r / 255f, g / 255f, b / 255f, 1f);

        var seconds = (parsed_value >> 24) / 1000f;

        ColorTools.ChangeColor(_background, color, seconds).GetAwaiter();
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
        var element = GetRenderable(placement);
        element?.SetValue(placement.Event, ValueTextCache, ValueChangeWrapMode.RemoveTexture);
    }

    private void StopEventHandler(Placement placement, int index)
    {
        var element = GetRenderable(placement);
        element?.SetValue(placement.Event, ValueTextCache, ValueChangeWrapMode.ResetToDefault);
    }

    private void DividerEventHandler(Placement placement, int index)
    {
        LastDividerIndex = placement.SequenceIndex;
    }

    private Vector3 UpdateStaticRenderables(int w, int h, float scale)
    {
        Camera.SetRenderScale(scale);
        StaticCamera.SetRenderScale(scale);
        scale = Math.Min(scale, 1f);
        var width_scale = Width / scale - Width;
        var height_scale = Height / scale - Height;

        var background = _background.GetScale();
        _background.SetScale((w + width_scale, h + height_scale, background.Z));
        _background.SetPosition((-width_scale / 2f, -height_scale / 2f, 0));

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

    private void FileDrop(string? location, bool reset_time)
    {
        _reset_time = reset_time;
        Camera.ScrollTo((0, -300, 0));

        var old_location = _sequence_location;
        if (location is null) return;

        Task.Run(async () =>
        {
            _log_text.SetTextContents("Loading...");
            await UpdateSequence(location, old_location != location || reset_time);
            _log_text.SetTextContents(string.Empty);
        }, Token);
    }

    protected void HandleZoomControl(float scale)
    {
        const float stepping = .05f;
        var camera_scale = Camera.GetRenderScale();
        Zoom = Math.Max(camera_scale + scale * stepping, stepping);
        UpdateStaticRenderables(Width, Height, Zoom);
        SetStatusMessage($"[Camera]: Setting zoom to: {Zoom:0.##%}");
    }

    protected bool IsSeekTimeoutPassed(int divide = 1)
    {
        const int seek_timeout = 250;
        return _seek_delay_stopwatch.ElapsedMilliseconds > seek_timeout / divide;
    }

    protected void RestartSeekTimer()
    {
        _seek_delay_stopwatch.Restart();
    }
}