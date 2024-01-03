using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.Fonts;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Resamplers;
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

public class ThirtyDollarApplication : IScene
{
    private static Texture? MissingTexture;
    private readonly List<Renderable> start_objects = new();
    private readonly List<Renderable> static_objects = new();
    private Memory<SoundRenderable> TDW_images;
    private readonly Stopwatch _open_stopwatch = new();
    private readonly Stopwatch _seek_delay_stopwatch = new();
    private int Video_I;

    public readonly SequencePlayer SequencePlayer;
    private readonly AudioContext? _context;

    private DollarStoreCamera Camera;
    private int Width;
    private int Height;
    private int PlayfieldWidth;

    private ColoredPlane _background = null!;
    private ColoredPlane _flash_overlay = null!;
    private ColoredPlane _visible_area = null!;
    private Renderable _greeting = null!;

    private Sequence _sequence = null!;
    private string? _sequence_location;
    private DateTime _sequence_date_modified = DateTime.MinValue;
    private readonly Stopwatch _file_modified_stopwatch = new();
    
    private Placement[] _placement = null!;
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
    
    public SampleHolder? SampleHolder { get; set; }
    public int RenderableSize { get; set; } = 64;
    public int MarginBetweenRenderables { get; set; } = 12;
    public int ElementsOnSingleLine { get; init; } = 16;
    public CameraFollowMode CameraFollowMode { get; set; } = CameraFollowMode.TDW_Like;
    public string? BackgroundVertexShaderLocation { get; init; }
    public string? BackgroundFragmentShaderLocation { get; init; }
    public Action<string> Log { get; init; } = log => { Console.WriteLine($"({DateTime.Now:G}): {log}"); };
    public float Scale { get; init; } = 1f;

    /// <summary>
    /// Creates a TDW sequence visualizer.
    /// </summary>
    /// <param name="width">The width of the visualizer.</param>
    /// <param name="height">The height of the visualizer.</param>
    /// <param name="sequenceLocation">The location of the sequence.</param>
    /// <param name="audio_context">The audio context the application will use.</param>
    public ThirtyDollarApplication(int width, int height, string? sequenceLocation, AudioContext? audio_context = null)
    {
        Width = width;
        Height = height;
        _sequence_location = sequenceLocation;
        
        var camera_position = new Vector3(0,-300f,0);
        Camera = new DollarStoreCamera(camera_position, new Vector2i(Width, Height));
        _open_stopwatch.Start();
        _seek_delay_stopwatch.Start();
        _file_modified_stopwatch.Start();

        _context = audio_context;
        SequencePlayer = new SequencePlayer();
    }

    /// <summary>
    /// This method loads the sequence, textures and sounds.
    /// </summary>
    /// <exception cref="Exception">Exception thrown when one of the arguments is invalid.</exception>
    public void Init(Manager manager)
    {
        Video_I = 0;
        SequencePlayer.Stop().GetAwaiter().GetResult();
        
        MissingTexture ??= new Texture("ThirtyDollarVisualizer.Assets.Textures.action_missing.png");
        if (!UpdatedRenderableScale)
        {
            RenderableSize = (int)(RenderableSize * Scale);
            MarginBetweenRenderables = (int)(MarginBetweenRenderables * Scale);
            UpdatedRenderableScale = true;
        }

        var tdw_images = new List<SoundRenderable>();
        
        Manager = manager;

        #region Textures

        Log("Loaded sequence and placement.");

        Shader? optional_shader = null;
        if (BackgroundVertexShaderLocation != null && BackgroundFragmentShaderLocation != null)
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

        var flex_box = new FlexBox(new Vector2i(LeftMargin + 7, 0),
            new Vector2i(PlayfieldWidth + MarginBetweenRenderables, Height), MarginBetweenRenderables);
        var wh = new Vector2i(RenderableSize, RenderableSize);

        Dictionary<string, Texture> texture_cache = new();
        Dictionary<string, Texture> volume_text_cache = new();

        _visible_area = new ColoredPlane(new Vector4(0, 0, 0, 0.25f), new Vector3(LeftMargin, -Height, 0.5f),
            new Vector2i(PlayfieldWidth, Height * 2));
        static_objects.Add(_visible_area);
        
        var font_family = Fonts.GetFontFamily();
        var greeting_font = font_family.CreateFont(36 * Scale, FontStyle.Bold);

        _greeting = new StaticText
        {
            FontStyle = FontStyle.Bold,
            FontSizePx = 36f,
            Value = "DON'T LECTURE ME WITH YOUR THIRTY DOLLAR VISUALIZER"
        }.WithPosition((Width / 2f, - 200f, 0.25f), PositionAlign.Center);
        
        start_objects.Add(_greeting);
        
        var font = font_family.CreateFont(16 * Scale, FontStyle.Bold);

        var volume_font = font_family.CreateFont(13 * Scale, FontStyle.Bold);
        var volume_color = new Rgba32(204, 204, 204, 1f);

        if (_sequence_location == null)
        {
            var dnd_texture = new Texture(greeting_font, "Drop a file on the window to start.");
            var drag_n_drop = new SoundRenderable(dnd_texture,
                new Vector3(Width / 2f - dnd_texture.Width / 2f, 0, 0.25f),
                new Vector2(dnd_texture.Width, dnd_texture.Height));
            
            start_objects.Add(drag_n_drop);
            drag_n_drop.UpdateModel(false);
            
            FinishedInitializing = true;
            _placement = Array.Empty<Placement>();
            return;
        }

        Task.Run(async () =>
        {
            var sample_holder = SampleHolder;
            if (sample_holder == null)
            {
                SampleHolder = sample_holder = new SampleHolder();
                sample_holder.DownloadUpdate = (sample, current, count) =>
                {
                    Log($"({current} - {count}): Downloading: \'{sample}\'");
                };

                await sample_holder.LoadSampleList();
                sample_holder.PrepareDirectory();
                await sample_holder.DownloadSamples();
                sample_holder.LoadSamplesIntoMemory();
                await sample_holder.DownloadImages();
            }
        }, Token).Wait(Token);
        
        var comp_location = _sequence_location;
        try
        {
            _sequence = Sequence.FromString(File.ReadAllText(comp_location));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return;
        }
        finally
        {
            _sequence_date_modified = File.GetLastWriteTime(_sequence_location);
        }
        
        tdw_images.EnsureCapacity(_sequence.Events.Length);

        var calculator = new PlacementCalculator(new EncoderSettings
        {
            SampleRate = TimingSampleRate,
            CombineDelayMs = 0,
            AddVisualEvents = true
        });

        _placement = calculator.Calculate(_sequence).ToArray();
        
        var i = 0ul;
        Task.Run(UpdateChecker, Token);

        try
        {
            foreach (var ev in _sequence.Events)
            {
                if (string.IsNullOrEmpty(ev.SoundEvent) || ev.SoundEvent.StartsWith('#'))
                {
                    i++;
                    continue;
                }

                CreateEventRenderable(tdw_images, ev, texture_cache, wh, flex_box, ValueTextCache, volume_text_cache, font,
                    volume_color, volume_font);
                i++;
            }

            var max_decreasing_event = _sequence.Events.Where(r => r.SoundEvent is "!stop" or "!loopmany").MaxBy(r => r.Value);
            
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
                ValueTextCache.Add("0", new Texture(font, "0"));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return;
        }
        finally
        {
            Log("Loaded textures.");
            Log("Loading sounds.");
            TDW_images = tdw_images.ToArray();
            Task.Run(async () => { await LoadAudio(); }, Token).Wait(Token);
        }

        #endregion
        
        Manager.CheckErrors();
        FinishedInitializing = true;
        return;

        async void UpdateChecker()
        {
            var len = (ulong)_sequence.Events.LongLength;
            var old = 0ul;

            ulong j;

            while ((j = i) < len)
            {
                if (j - old > 64)
                {
                    Console.Clear();
                    Console.WriteLine($"({j}) - ({_sequence.Events.LongLength}) ");
                    old = j;
                }

                Console.Write(new string('-', (int)(j - old)));
                await Task.Delay(33, Token);
            }
        }
    }

    /// <summary>
    /// Creates a Thirty Dollar Website renderable with the texture of the event and it's value and volume as children.
    /// </summary>
    /// <param name="tdw_images">The renderable list you want to add to.</param>
    /// <param name="ev">The event.</param>
    /// <param name="texture_cache">The texture cache.</param>
    /// <param name="wh">The dimensions of a single event.</param>
    /// <param name="flex_box">A flexbox orderer.</param>
    /// <param name="value_text_cache">The cache for the textures of the values.</param>
    /// <param name="volume_text_cache">The cache for the textures of the elements' volumes.</param>
    /// <param name="font">The font you want to use for the generated textures.</param>
    /// <param name="volume_color">The color of the volume font.</param>
    /// <param name="volume_font">The volume font.</param>
    /// <exception cref="Exception"></exception>
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
                value_text_cache.Add(value, value_texture);
            }
        }

        if (value_texture != null)
        {
            var text_position = new Vector3
            {
                X = plane_position.X + width_height.X / 2f - value_texture.Width / 2f,
                Y = box_position.Y + RenderableSize - 10f * Scale,
                Z = box_position.Z
            };
            text_position.Z -= 0.1f;

            var text = new TexturedPlane(value_texture, text_position, (value_texture.Width, value_texture.Height));
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

    private async Task LoadAudio()
    {
        if (_context == null) return;
        var holder = new BufferHolder(new Dictionary<string, Dictionary<double, AudibleBuffer>>());
        
        var pcm_encoder = new PcmEncoder(SampleHolder ?? throw new Exception("Sample holder is null."), new EncoderSettings
        {
            SampleRate = (uint)_context.SampleRate,
            Channels = 2,
            CutDelayMs = 0,
            Resampler = new LinearResampler()
        }, Log);

        var (processed_samples, _) = await pcm_encoder.GetAudioSamples(-1, _placement, CancellationToken.None);
        _context.GlobalVolume = .25f;

        foreach (var ev in processed_samples)
        {
            var value = ev.Value;
            var name = ev.Name;

            if (holder.ProcessedBuffers.TryGetValue(name, out var value_dictionary))
            {
                if (value_dictionary.ContainsKey(value)) continue;
            }

            if (value_dictionary == null)
            {
                value_dictionary = new Dictionary<double, AudibleBuffer>();
                holder.ProcessedBuffers.Add(name, value_dictionary);
            }

            var sample = _context.GetBufferObject(ev.AudioData, _context.SampleRate);
            value_dictionary.Add(value, sample);
        }

        await SequencePlayer.UpdateSequence(holder, new TimedEvents
        {
            Placement = _placement,
            TimingSampleRate = TimingSampleRate
        });
        
        await SequencePlayer.Start();
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

        var greeting_translation = _greeting.GetTranslation();
        _greeting.SetTranslation(greeting_translation - new Vector3((Width - w) / 2f, (Height - h) / 2f, 0));
        
        var visible_position = _visible_area.GetPosition();
        visible_position.X = w / 2f - visible.X / 2;
        visible_position.Y = -h;
        _visible_area.SetPosition(visible_position);

        var current_margin = visible_position.X;
        
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
        SequencePlayer.RestartAfter(3000).GetAwaiter().GetResult();
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
        HandleSequenceUpdate();
        //Log($"Timing stopwatch: {_timing_stopwatch.Elapsed} / Placement: {_placement.Length} / Audio: {Audio_I} / Video: {Video_I}");

        var stopwatch = SequencePlayer.GetTimingStopwatch();
        
        for (; Video_I < _placement.LongLength; Video_I++)
        {
            Camera.Update();

            if (BackingAudio != null)
            {
                BackingAudio.UpdatePlayState(stopwatch.IsRunning);
                BackingAudio.SyncTime(stopwatch.Elapsed);
            } 
            
            var placement = _placement[Video_I];

            var len = TDW_images.Length;
            var placement_idx = (int) placement.SequenceIndex;
            var element = placement_idx > len || placement_idx < 0 ? null : TDW_images.Span[placement_idx];
            if (element == null) break;

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

            if (placement.Index > (ulong)((float)stopwatch.ElapsedMilliseconds * TimingSampleRate / 1000)) return;
            
            if (placement.Event.SoundEvent?.StartsWith('!') ?? false)
            {
                element.Fade();
                element.Expand();
            }
            else if (placement.Event.SoundEvent is not "#!cut")
            {
                element.Bounce();
            }

            switch (placement.Event.SoundEvent)
            {
                case "!speed":
                {
                    var val = (float) placement.Event.Value;
                    
                    LastBPM = placement.Event.ValueScale switch
                    {
                        ValueScale.None => val,
                        ValueScale.Add => LastBPM + val,
                        ValueScale.Times => LastBPM * val,
                        
                        _ => LastBPM
                    };
                    
                    break;
                }
                
                case "!bg":
                {
                    var parsed_value = (long)placement.Event.Value;

                    var r = (byte)parsed_value;
                    var g = (byte)(parsed_value >> 8);
                    var b = (byte)(parsed_value >> 16);
                    var color = new Vector4(r / 255f, g / 255f, b / 255f, 1f);

                    var seconds = (parsed_value >> 24) / 1000f;

                    ColorTools.ChangeColor(_background, color, seconds).GetAwaiter();
                    break;
                }

                case "!flash":
                {
                    Task.Run(async () =>
                    {
                        await ColorTools.ChangeColor(_flash_overlay, new Vector4(1, 1, 1, 1), 0.125f);
                        await ColorTools.ChangeColor(_flash_overlay, new Vector4(0, 0, 0, 0), 0.25f);
                    }, Token);
                    break;
                }

                case "!pulse":
                {
                    var parsed_value = (long) placement.Event.Value;
                    var repeats = (byte)parsed_value;
                    float frequency = (short)(parsed_value >> 8);
                    
                    Camera.Pulse(repeats, frequency * 1000f / (LastBPM / 60));
                    break;
                }

                case "!loopmany":
                {
                    element.SetValue(placement.Event, ValueTextCache, ValueChangeWrapMode.RemoveTexture);
                    break;
                }
                
                case "!stop":
                {
                    element.SetValue(placement.Event, ValueTextCache, ValueChangeWrapMode.ResetToDefault);
                    break;
                }
            }
        }
        
    }

    private void HandleSequenceUpdate()
    {
        const int update_frequency_ms = 100;
        if (_file_modified_stopwatch.ElapsedMilliseconds < update_frequency_ms) return;
        if (_sequence_location == null) return;
        
        var m_date = File.GetLastWriteTime(_sequence_location);
        if (m_date.Equals(_sequence_date_modified)) return;

        Log("Change detected in sequence. Updating.");

        _sequence_date_modified = m_date;
        
        Manager.RenderBlock.Wait(Token);
        try
        {
            FileDrop(_sequence_location, false);
        }
        finally
        {
            Manager.RenderBlock.Release();
        }
        
        var placement = _placement[Math.Clamp(SequencePlayer.PlacementIndex - 1, 0, _placement.Length - 1)];
        var placement_index = placement.Index;
            
        SequencePlayer.RestartAfter((long) placement_index * 1000 / TimingSampleRate).GetAwaiter().GetResult();
        _file_modified_stopwatch.Restart();
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
        if (reset_time)
        {
            Camera = new DollarStoreCamera((0, -300f, 0), (Width, Height));
            SequencePlayer.Restart().GetAwaiter().GetResult();
            Video_I = 0;
        }

        TDW_images = new Memory<SoundRenderable>();
        static_objects.Clear();
        start_objects.Clear();

        var old_location = _sequence_location;
        _sequence_location = location;
        Init(Manager);
        
        Resize(Width, Height);
        
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
            
            var (placement, i) = _placement.Select((placement, i) => (placement , i))
                .MinBy(stack => Math.Abs((long) stack.placement.Index * 1000 / TimingSampleRate - change));
            var placement_index = placement?.Index ?? 0;
            
            await SequencePlayer.Seek((long) placement_index * 1000 / TimingSampleRate);
            Video_I = i;
        }

        if (state.IsKeyDown(Keys.Right) && _seek_delay_stopwatch.ElapsedMilliseconds > seek_timeout)
        {
            _seek_delay_stopwatch.Restart();
            var change = elapsed + seek_length;
            
            var (placement, i) = _placement.Select((placement, i) => (placement , i))
                .MinBy(stack => Math.Abs((long) stack.placement.Index * 1000 / TimingSampleRate - change));
            var placement_index = placement?.Index ?? 0;
            
            await SequencePlayer.Seek((long) placement_index * 1000 / TimingSampleRate);
            Video_I = i;
        }

        if (!state.IsKeyPressed(Keys.R)) return;
        FileDrop(_sequence_location);
    }
}
