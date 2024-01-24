using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.Fonts;
using ThirtyDollarConverter.Objects;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarEncoder.Wave;
using ThirtyDollarParser;
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
    private static Texture? MissingTexture;
    private readonly List<Renderable> start_objects = new();
    private readonly List<Renderable> static_objects = new();
    private Memory<SoundRenderable>  TDW_images;
    private readonly Stopwatch _open_stopwatch = new();
    private readonly Stopwatch _seek_delay_stopwatch = new();

    private DollarStoreCamera Camera;
    private int Width;
    private int Height;
    private int PlayfieldWidth;

    private ColoredPlane _background = null!;
    private ColoredPlane _flash_overlay = null!;
    private ColoredPlane _visible_area = null!;
    private Renderable _greeting = null!;
    private SoundRenderable _drag_n_drop = null!;
    
    private const int TimingSampleRate = 100_000;

    private CancellationToken Token => TokenSource.Token;
    private readonly CancellationTokenSource TokenSource = new();
    private Manager Manager = null!;

    // This is currently a hack, but I can't think of any other way to fix this without restructuring the code.
    private int DividerCount;
    private bool FinishedInitializing;
    private int LeftMargin;
    private long CurrentResizeFrame;
    private ulong LastDividerIndex;
    private bool UpdatedRenderableScale;

    private BackingAudio? BackingAudio;
    
    // These are needed for some events, because I don't want to pollute the placement events. They're polluted enough as they are.
    private float LastBPM = 300f;
    private readonly Dictionary<string, Texture> ValueTextCache = new();
    private bool _reset_time;
    private Dictionary<string, Texture> _texture_cache = null!;
    private Dictionary<string, Texture> _volume_text_cache = null!;
    public int RenderableSize { get; set; } = 64;
    public int MarginBetweenRenderables { get; set; } = 12;
    public int ElementsOnSingleLine { get; init; } = 16;
    public CameraFollowMode CameraFollowMode { get; set; } = CameraFollowMode.TDW_Like;
    public string? BackgroundVertexShaderLocation { get; init; }
    public string? BackgroundFragmentShaderLocation { get; init; }
    public float Scale { get; init; } = 1f;

    /// <summary>
    /// Creates a TDW sequence visualizer.
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
        
        Camera = new DollarStoreCamera((0,-300f,0), new Vector2i(Width, Height));
        _open_stopwatch.Start();
        _seek_delay_stopwatch.Start();

        _reset_time = true;
    }

    /// <summary>
    /// This method loads the sequence, textures and sounds.
    /// </summary>
    /// <exception cref="Exception">Exception thrown when one of the arguments is invalid.</exception>
    public void Init(Manager manager)
    {
        if (_reset_time)
            SequencePlayer.Stop().GetAwaiter().GetResult();
        
        static_objects.Clear();
        start_objects.Clear();
        
        if (!UpdatedRenderableScale)
        {
            RenderableSize = (int)(RenderableSize * Scale);
            MarginBetweenRenderables = (int)(MarginBetweenRenderables * Scale);
            UpdatedRenderableScale = true;
        }

        Manager = manager;
        if (MissingTexture is null)
        {
            MissingTexture = new Texture("ThirtyDollarVisualizer.Assets.Textures.action_missing.png");
            Manager.QueueTexture(MissingTexture);
        }

        Log("Loaded sequence and placement.");

        Shader? optional_shader = null;
        if (BackgroundVertexShaderLocation is not null && BackgroundFragmentShaderLocation is not null)
        {
            optional_shader = new Shader(BackgroundVertexShaderLocation, BackgroundFragmentShaderLocation);
        }

        _background = new ColoredPlane(new Vector4(0.21f, 0.22f, 0.24f, 1f), new Vector3(-Width, -Height, 1f),
            new Vector2(Width * 2, Height * 2),
            optional_shader);
        
        _flash_overlay = new ColoredPlane(new Vector4(1f, 1f, 1f, 0f), new Vector3(-Width, -Height, 0.75f),
            new Vector2(Width * 2, Height * 2));

        static_objects.Add(_background);
        static_objects.Add(_flash_overlay);

        PlayfieldWidth = ElementsOnSingleLine * (RenderableSize + MarginBetweenRenderables) + MarginBetweenRenderables +
                         (int) (15 * Scale) /*px Padding in the site. */;

        LeftMargin = (int)((float)Width / 2 - (float) PlayfieldWidth / 2);

        _visible_area = new ColoredPlane(new Vector4(0, 0, 0, 0.25f), new Vector3(LeftMargin, -Height, 0.5f),
            new Vector2i(PlayfieldWidth, Height * 2));
        static_objects.Add(_visible_area);
        
        var font_family = Fonts.GetFontFamily();
        var greeting_font = font_family.CreateFont(36 * Scale, FontStyle.Bold);

        var greeting = new StaticText
        {
            FontStyle = FontStyle.Bold,
            FontSizePx = 36f * Scale,
            Value = "DON'T LECTURE ME WITH YOUR THIRTY DOLLAR VISUALIZER"
        };
        Manager.QueueTexture(greeting.GetTexture()!);

        _greeting = greeting.WithPosition((Width / 2f, -200f, 0.25f), PositionAlign.Center);
        
        start_objects.Add(_greeting);

        if (_sequence_location == null)
        {
            var dnd_texture = new Texture(greeting_font, "Drop a file on the window to start.");
            Manager.QueueTexture(dnd_texture);
            _drag_n_drop = new SoundRenderable(dnd_texture,
                new Vector3(Width / 2f - dnd_texture.Width / 2f, 0, 0.25f),
                new Vector2(dnd_texture.Width, dnd_texture.Height));
            
            _greeting.Children.Add(_drag_n_drop);
            start_objects.Add(_drag_n_drop);
            _drag_n_drop.UpdateModel(false);
            
            FinishedInitializing = true;
            return;
        }
        
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
        FinishedInitializing = true;
    }
    
    protected override void HandleAfterSequenceUpdate(TimedEvents events)
    {
        Manager.RenderBlock.Wait(Token);
        FinishedInitializing = false;
        _drag_n_drop.IsVisible = false;
        Camera = new DollarStoreCamera((0,-300f,0), new Vector2i(Width, Height));
        
        var tdw_images = new List<SoundRenderable>();
        var font_family = Fonts.GetFontFamily();
        
        var flex_box = new FlexBox(new Vector2i((int)(LeftMargin + 7 * Scale), 0),
            new Vector2i(PlayfieldWidth + MarginBetweenRenderables, Height), MarginBetweenRenderables);
        var wh = new Vector2i(RenderableSize, RenderableSize);

        _texture_cache = new Dictionary<string, Texture>();
        _volume_text_cache = new Dictionary<string, Texture>();
        
        var font = font_family.CreateFont(16 * Scale, FontStyle.Bold);

        var volume_font = font_family.CreateFont(13 * Scale, FontStyle.Bold);
        var volume_color = new Rgba32(204, 204, 204, 1f);

        try
        {
            foreach (var placement in events.Placement)
            {
                var ev = placement.Event;
                if (string.IsNullOrEmpty(ev.SoundEvent) || ev.SoundEvent.StartsWith('#'))
                {
                    continue;
                }

                try
                {
                    CreateEventRenderable(tdw_images, ev, _texture_cache, wh, flex_box, 
                        ValueTextCache, _volume_text_cache, font,
                        volume_color, volume_font);
                }
                finally
                {
                    Manager.CheckErrors();
                }
            }

            var max_decreasing_event = events.Sequence.Events.Where(r => r.SoundEvent is "!stop" or "!loopmany").MaxBy(r => r.Value);
            
            var textures = max_decreasing_event?.Value ?? 0;

            for (var val = (int)textures - 1; val >= 0; val--)
            {
                var search = val.ToString();
                if (ValueTextCache.ContainsKey(search)) continue;

                var texture = new Texture(font, search);
                Manager.QueueTexture(texture);
                ValueTextCache.Add(search, texture);
            }

            if (!ValueTextCache.ContainsKey("0"))
            {
                var texture = new Texture(font, "0");
                Manager.QueueTexture(texture);
                ValueTextCache.Add("0", texture);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        TDW_images = tdw_images.ToArray();

        SequencePlayer.Start().GetAwaiter().GetResult();
        FinishedInitializing = true;
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

    /// <summary>
    /// Creates a Thirty Dollar Website renderable with the texture of the event and its value and volume as children.
    /// </summary>
    private void CreateEventRenderable(ICollection<SoundRenderable> tdw_images, Event ev, IDictionary<string, Texture> texture_cache, Vector2i wh,
        FlexBox flex_box,
        IDictionary<string, Texture> value_text_cache, IDictionary<string, Texture> volume_text_cache, Font font, Rgba32 volume_color, Font volume_font)
    {
        var image = $"{SampleHolder!.DownloadLocation}/Images/" + ev.SoundEvent?.Replace("!", "action_") + ".png";

        if (!File.Exists(image))
        {
            if (MissingTexture == null) throw new Exception("Texture for missing elements isn't loaded.");
            Log($"Asset: \'{image}\' not found.");
            texture_cache.TryAdd(image, MissingTexture);
        }

        texture_cache.TryGetValue(image, out var texture);
        if (texture == null)
        {
            texture = new Texture(image);
            Manager.QueueTexture(texture);
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
        if (ev.Value > 0 && !(ev.SoundEvent!.StartsWith('!') || ev.SoundEvent!.StartsWith('_')))
        {
            value = "+" + value;
        }

        value = ev.ValueScale switch
        {
            ValueScale.Add => "+" + value,
            ValueScale.Times => "×" + value,
            ValueScale.Divide => "/" + value,
            _ => value
        };

        switch (ev.SoundEvent)
        {
            case "!bg":
            {
                var seconds = ((long)ev.Value >> 24) / 1000f;
                value = seconds.ToString("0.##");
                break;
            }
            
            case "!volume":
                value += "%";
                break;
            
            case "!pulse":
                var parsed_value = (long) ev.Value;
                var repeats = (byte)parsed_value;
                var pulse_times = (short)(parsed_value >> 8);

                value = $"{repeats}, {pulse_times}";
                break;
        }

        Texture? value_texture = null;

        if (ev.Value != 0 && ev.SoundEvent is not "_pause" || ev.SoundEvent is "!transpose")
        {
            value_text_cache.TryGetValue(value, out value_texture);
            if (value_texture == null)
            {
                value_texture = new Texture(font, value, volume_color);
                Manager.QueueTexture(texture);
                value_text_cache.Add(value, value_texture);
            }
        }

        if (value_texture is not null)
        {
            var text_position = new Vector3
            {
                X = plane_position.X + width_height.X / 2f,
                Y = box_position.Y + RenderableSize - MarginBetweenRenderables + 1 * Scale,
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
            var volume_text = volume.ToString("0.##") + "%";

            volume_text_cache.TryGetValue(volume_text, out var volume_texture);
            if (volume_texture == null)
            {
                volume_texture = new Texture(volume_font, volume_text);
                Manager.QueueTexture(texture);
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
        
        tdw_images.Add(plane);

        if (ev.SoundEvent is not "!divider") return;

        flex_box.NewLine();
        flex_box.NewLine();
        DividerCount++;
    }

    private SoundRenderable? GetRenderable(Placement placement)
    {
        var len = TDW_images.Length;
        var placement_idx = (int) placement.SequenceIndex;
        var element = placement_idx > len || placement_idx < 0 ? null : TDW_images.Span[placement_idx];

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
            case CameraFollowMode.TDW_Like when SequencePlayer.GetTimingStopwatch().IsRunning:
            {
                float margin = RenderableSize;
                    
                if (!Camera.IsOutsideOfCameraView(position, scale, margin) && placement.Event.SoundEvent is not "!divider") break;
                    
                Camera.ScrollTo(new Vector3(0, position.Y - margin, 0f));
                break;
            }

            case CameraFollowMode.Current_Line when SequencePlayer.GetTimingStopwatch().IsRunning:
            {
                Camera.ScrollTo(position * Vector3.UnitY - Vector3.UnitY * (Height / 2f));
                break;
            }
        }
    }

    private void NormalSubscription(Placement placement, int index)
    {
        CameraBoundsCheck(placement);
        var element = GetRenderable(placement);
        if (element == null) return;
        
        if (placement.Event.SoundEvent?.StartsWith('!') ?? false)
        {
            element.Fade();
            element.Expand();
        }
        else if (placement.Event.SoundEvent is not "#!cut")
        {
            element.Bounce();
        }
    }

    private void SpeedEventHandler(Placement placement, int index)
    {
        var val = (float) placement.Event.Value;
                    
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
        var parsed_value = (long) placement.Event.Value;
        var repeats = (byte)parsed_value;
        float frequency = (short)(parsed_value >> 8);
                    
        Camera.Pulse(repeats, frequency * 1000f / (LastBPM / 60));
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

    public void Resize(int w, int h)
    {
        var resize = new Vector2i(w, h);

        Camera.Viewport = resize;
        GL.Viewport(0, 0, w, h);
        
        Camera.UpdateMatrix();

        if (!FinishedInitializing) return;

        var background = _background.GetScale();
        _background.SetScale((w * 2, h * 2, background.Z));
        _background.SetPosition((-w, -h, 0));

        var flash = _flash_overlay.GetScale();
        _flash_overlay.SetScale((w * 2, h * 2, flash.Z));
        _flash_overlay.SetPosition((-w, -h, 0));
        
        var visible = _visible_area.GetScale();
        _visible_area.SetScale((visible.X, h * 2, visible.Z));
        
        var visible_position = _visible_area.GetPosition();
        visible_position.X = w / 2f - visible.X / 2;
        visible_position.Y = -h;
        _visible_area.SetPosition(visible_position);

        var current_margin = visible_position.X;
        
        foreach (var r in start_objects)
        {
            var pos = r.GetPosition();
            var delta_x = (Width - w) / 2f;

            pos -= delta_x * Vector3.UnitX;
            r.SetPosition(pos);
        }
        
        Width = w;
        Height = h;
        
        var current_update = CurrentResizeFrame = _open_stopwatch.ElapsedMilliseconds;
        
        Task.Run(() =>
        {
            foreach (var image in TDW_images.Span)
            {
                if (CurrentResizeFrame != current_update) break;
                
                var original_offset = image.GetTranslation();
                var new_offset = new Vector3(original_offset)
                {
                    X = current_margin - LeftMargin
                };

                image.SetTranslation(new_offset);
            }
        }, Token);
    }

    public void Start()
    {
        if (_sequence_location == null) return;
        SequencePlayer.Stop().GetAwaiter();
        Task.Run(start, Token);
        return;

        async void start()
        {
            await SequencePlayer.RestartAfter(3000);
        }
    }

    public void Render()
    {
        if (!FinishedInitializing) return;

        var camera_x = Camera.Position.X;
        var camera_y = Camera.Position.Y;
        var camera_xw = camera_x + Width;
        var camera_yh = camera_y + Height;
        
        foreach (var renderable in static_objects)
        {
            Manager.CheckErrors();
            
            var new_position = new Vector3(renderable.GetTranslation())
            {
                Y = Camera.Position.Y + Height
            };

            renderable.SetTranslation(new_position);
            
            renderable.Render(Camera);
        }

        var size_renderable = RenderableSize + MarginBetweenRenderables;
        var repeats_renderable = PlayfieldWidth / size_renderable;

        var dividers_size = repeats_renderable * DividerCount * 2;
        var tdw_span = TDW_images.Span;

        var new_start =
            Math.Max(Math.Max((int)camera_y / size_renderable, 0) * repeats_renderable - dividers_size, 0);
        var new_end = Math.Min(TDW_images.Length,
            (int)(repeats_renderable * (camera_y / size_renderable) +
                  (int)(repeats_renderable * ((float)Height / size_renderable) * 1.25f)));

        for (var i = new_start; i < new_end; i++)
        {
            var renderable = tdw_span[i];
            RenderRenderable(renderable);
        }
        
        foreach (var renderable in start_objects)
        {
            RenderRenderable(renderable);
        }

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

    public void Update()
    {
        if (!FinishedInitializing) return;
        HandleIfSequenceUpdate();
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

    private void UpdateBackingTrack(string location)
    {
        var decoder = new WaveDecoder();
        var file_stream = File.OpenRead(location);
        var pcm_data = decoder.Read(file_stream);

        var audio = pcm_data.ReadAsFloat32Array(true);

        if (audio == null) return;
        BackingAudio = new BackingAudio(SequencePlayer.GetContext(), audio, (int) pcm_data.SampleRate);
        BackingAudio.Play();
    }

    private void FileDrop(string? location, bool reset_time)
    {
        _reset_time = reset_time;
        if (reset_time)
        {
            Camera = new DollarStoreCamera((0, -300f, 0), (Width, Height));
        }
        
        var old_location = _sequence_location;
        if (location is not null)
            UpdateSequence(location).GetAwaiter().GetResult();
        
        if (old_location != location || reset_time)
            Start();
    }
    
    public void Mouse(MouseState state)
    {
        var scroll = state.ScrollDelta;
        if (scroll != Vector2.Zero)
        {
            var new_delta = Vector3.UnitY * (scroll.Y * 100f);
            
            Camera.ScrollDelta(new_delta);
            Camera.Update();
        }
    }

    public async void Keyboard(KeyboardState state)
    {
        const int seek_timeout = 250;
        const int seek_length = 1000;
        var stopwatch = SequencePlayer.GetTimingStopwatch();
        
        switch (state.IsKeyPressed(Keys.Space))
        {
            case true:
                SequencePlayer.TogglePause();
                break;
        }

        CameraFollowMode = state.IsKeyPressed(Keys.C) switch
        {
            true when CameraFollowMode is CameraFollowMode.Current_Line => CameraFollowMode.TDW_Like,
            true when CameraFollowMode is CameraFollowMode.TDW_Like => CameraFollowMode.None,
            true when CameraFollowMode is CameraFollowMode.None => CameraFollowMode.Current_Line,
            _ => CameraFollowMode
        };

        var elapsed = stopwatch.ElapsedMilliseconds;
        if (state.IsKeyDown(Keys.Left) && _seek_delay_stopwatch.ElapsedMilliseconds > seek_timeout)
        {
            _seek_delay_stopwatch.Restart();
            var change = elapsed - seek_length;
            
            var (placement, i) = TimedEvents.Placement.Select((placement, i) => (placement , i))
                .MinBy(stack => Math.Abs((long) stack.placement.Index * 1000 / TimingSampleRate - change));
            var placement_index = placement?.Index ?? 0;
            
            await SequencePlayer.Seek((long) placement_index * 1000 / TimingSampleRate);
        }

        if (state.IsKeyDown(Keys.Right) && _seek_delay_stopwatch.ElapsedMilliseconds > seek_timeout)
        {
            _seek_delay_stopwatch.Restart();
            var change = elapsed + seek_length;
            
            var (placement, i) = TimedEvents.Placement.Select((placement, i) => (placement , i))
                .MinBy(stack => Math.Abs((long) stack.placement.Index * 1000 / TimingSampleRate - change));
            var placement_index = placement?.Index ?? 0;
            
            await SequencePlayer.Seek((long) placement_index * 1000 / TimingSampleRate);
        }

        if (!state.IsKeyPressed(Keys.R)) return;
        FileDrop(_sequence_location, true);
    }
}
