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
using ThirtyDollarVisualizer.Helpers.Timing;
using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Objects.Settings;
using ThirtyDollarVisualizer.Objects.Text;

namespace ThirtyDollarVisualizer.Scenes;

public class ThirtyDollarApplication : IScene
{
    private static readonly List<Renderable> start_objects = new();
    private static readonly List<Renderable> static_objects = new();
    private static readonly List<SoundRenderable> tdw_images = new();
    private readonly SeekableStopwatch _timing_stopwatch = new();
    private readonly Stopwatch _open_stopwatch = new();
    private readonly Stopwatch _seek_stopwatch = new();
    private TimeSpan _open_time;
    private int Video_I;

    private readonly AudioContext AudioContext = new();
    private readonly Dictionary<string, Dictionary<double, AudibleBuffer>> ProcessedBuffers = new();
    private readonly List<AudibleBuffer> ActiveSamples = new();
    private int Audio_I;

    private static DollarStoreCamera Camera = null!;
    private int Width;
    private int Height;
    private int PlayfieldWidth;

    private ColoredPlane _background = null!;
    private ColoredPlane _flash_overlay = null!;
    private ColoredPlane _visible_area = null!;
    private TexturedPlane _greeting = null!;

    private Composition _composition = null!;
    private string? _composition_location;
    private DateTime _composition_date_modified = DateTime.MinValue;
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
    private bool OpenAudioHandler;
    private bool UpdatedRenderableScale;

    private BackingAudio? BackingAudio;
    
    // These are needed for some events, because I don't want to pollute the placement events. They're polluted enough as they are.
    private float LastBPM = 300f;
    private readonly Dictionary<string, Texture> ValueTextCache = new();
    
    public bool PlayAudio { get; init; }
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
    /// Creates a composition visualizer.
    /// </summary>
    /// <param name="width">The width of the visualizer.</param>
    /// <param name="height">The height of the visualizer.</param>
    /// <param name="composition_location">The location of the composition.</param>
    public ThirtyDollarApplication(int width, int height, string? composition_location)
    {
        Width = width;
        Height = height;
        _composition_location = composition_location;
        
        var camera_position = new Vector3(0,-300f,0);
        Camera = new DollarStoreCamera(camera_position, new Vector2i(Width, Height));
        _open_stopwatch.Start();
        _seek_stopwatch.Start();
        _file_modified_stopwatch.Start();
        
        AudioContext.Destroy();
        AudioContext.Create();
    }

    /// <summary>
    /// This method loads the composition, textures and sounds.
    /// </summary>
    /// <exception cref="Exception">Exception thrown when one of the arguments is invalid.</exception>
    public void Init(Manager manager)
    {
        _open_time = _open_stopwatch.Elapsed;
        if (!UpdatedRenderableScale)
        {
            RenderableSize = (int)(RenderableSize * Scale);
            MarginBetweenRenderables = (int)(MarginBetweenRenderables * Scale);
            UpdatedRenderableScale = true;
        }
        
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

        var greeting_texture = new Texture(greeting_font, "DON'T LECTURE ME WITH YOUR THIRTY DOLLAR VISUALIZER");
        var moai = new Texture("ThirtyDollarVisualizer.Assets.Textures.moai.png");

        moai.Width = (int)(moai.Width * Scale);
        moai.Height = (int)(moai.Height * Scale);
        
        _greeting = new TexturedPlane(greeting_texture,
            new Vector3(Width / 2f - greeting_texture.Width / 2f, -200, 0.25f),
            (greeting_texture.Width, greeting_texture.Height));
        
        _greeting.Children.Add(new TexturedPlane(moai, new Vector3(Width / 2f - greeting_texture.Width / 2f - greeting_texture.Height, -200, 0.25f), 
            new Vector2(greeting_texture.Height,greeting_texture.Height)));
        
        _greeting.Children.Add(new TexturedPlane(moai, new Vector3(Width / 2f + greeting_texture.Width / 2f, -200, 0.25f), 
            new Vector2(greeting_texture.Height,greeting_texture.Height)));
        
        start_objects.Add(_greeting);
        
        var font = font_family.CreateFont(16 * Scale, FontStyle.Bold);

        var volume_font = font_family.CreateFont(13 * Scale, FontStyle.Bold);
        var volume_color = new Rgba32(204, 204, 204, 1f);

        if (_composition_location == null)
        {
            var dnd_texture = new Texture(greeting_font, "Drop a file on the window to start.");
            var drag_n_drop = new SoundRenderable(dnd_texture,
                new Vector3(Width / 2f - dnd_texture.Width / 2f, 0, 0.25f),
                new Vector2(dnd_texture.Width, dnd_texture.Height));
            
            tdw_images.Add(drag_n_drop);
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
                if (!sample_holder.DownloadedAllFiles())
                {
                    await sample_holder.DownloadSamples();
                }
                sample_holder.LoadSamplesIntoMemory();
                await sample_holder.DownloadImages();
            }
        }, Token).Wait(Token);
        
        var comp_location = _composition_location;
        try
        {
            _composition = Composition.FromString(File.ReadAllText(comp_location));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return;
        }
        finally
        {
            _composition_date_modified = File.GetLastWriteTime(_composition_location);
        }
        
        tdw_images.EnsureCapacity(_composition.Events.Length);

        var calculator = new PlacementCalculator(new EncoderSettings
        {
            SampleRate = TimingSampleRate,
            CombineDelayMs = 0,
            AddVisualEvents = true
        });

        _placement = calculator.Calculate(_composition).ToArray();
        
        var i = 0ul;
        Task.Run(UpdateChecker, Token);

        try
        {
            foreach (var ev in _composition.Events)
            {
                if (string.IsNullOrEmpty(ev.SoundEvent) || ev.SoundEvent.StartsWith("#"))
                {
                    i++;
                    continue;
                }

                CreateEventRenderable(ev, texture_cache, wh, flex_box, ValueTextCache, volume_text_cache, font,
                    volume_color, volume_font);
                i++;
            }

            var max_decreasing_event = _composition.Events.MaxBy(r =>
            {
                if (r.SoundEvent is not "!stop" and not "!loopmany") return 0;
                return r.Value;
            });

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
            Task.Run(async () => { await LoadAudio(); }, Token).Wait(Token);
        }

        #endregion
        
        Manager.CheckErrors();
        FinishedInitializing = true;
        return;

        async void UpdateChecker()
        {
            var len = (ulong)_composition.Events.LongLength;
            var old = 0ul;

            ulong j;

            while ((j = i) < len)
            {
                if (j - old > 64)
                {
                    Console.Clear();
                    Console.WriteLine($"({j}) - ({_composition.Events.LongLength}) ");
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
    private void CreateEventRenderable(Event ev, IDictionary<string, Texture> texture_cache, Vector2i wh,
        FlexBox flex_box,
        IDictionary<string, Texture> value_text_cache, IDictionary<string, Texture> volume_text_cache, Font font, Rgba32 volume_color, Font volume_font)
    {
        var image = $"{SampleHolder!.DownloadLocation}/Images/" + ev.SoundEvent?.Replace("!", "action_") + ".png";

        if (!File.Exists(image))
        {
            throw new Exception($"Image asset for event \'{ev.SoundEvent}\' not found.");
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

        if (ev.Value != 0 && ev.SoundEvent is not "_pause")
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

            Texture? volume_texture = null;

            if (ev.Value != 0)
            {
                volume_text_cache.TryGetValue(volume_text, out volume_texture);
                if (volume_texture == null)
                {
                    volume_texture = new Texture(volume_font, volume_text);
                    volume_text_cache.Add(volume_text, volume_texture);
                }
            }

            if (volume_texture != null)
            {
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
        ProcessedBuffers.Clear();
        
        var pcm_encoder = new PcmEncoder(SampleHolder ?? throw new Exception("Sample holder is null."), new EncoderSettings
        {
            SampleRate = (uint)AudioContext.SampleRate,
            Channels = 2,
            CutDelayMs = 250,
            Resampler = new LinearResampler()
        }, Log);

        var (processed_samples, _) = await pcm_encoder.GetAudioSamples(-1, _placement, CancellationToken.None);
        AudioContext.GlobalVolume = .25f;

        foreach (var ev in processed_samples)
        {
            var value = ev.Value;
            var name = ev.Name;

            if (ProcessedBuffers.TryGetValue(name, out var value_dictionary))
            {
                if (value_dictionary.ContainsKey(value)) continue;
            }

            if (value_dictionary == null)
            {
                value_dictionary = new Dictionary<double, AudibleBuffer>();
                ProcessedBuffers.Add(name, value_dictionary);
            }

            var sample = new AudibleBuffer(ev.AudioData, AudioContext.SampleRate);

            value_dictionary.Add(value, sample);
        }
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
            foreach (var image in tdw_images)
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
        if (_composition_location == null) return;

        var current_open_time = _open_time;
        
        Task.Run(async () =>
        {
            await Task.Delay(3000, Token);
            if (current_open_time != _open_time) return;
            
            _timing_stopwatch.Restart();
            AudioHandler();
        }, Token);
    }

    public void Render()
    {
        if (!FinishedInitializing) return;
        
        foreach (var renderable in static_objects)
        {
            Manager.CheckErrors();
            
            var new_position = new Vector3(renderable.GetTranslation())
            {
                Y = Camera.Position.Y + Height / 2f
            };

            renderable.SetTranslation(new_position);
            
            renderable.Render(Camera);
        }

        var size_renderable = RenderableSize + MarginBetweenRenderables;
        var repeats_renderable = PlayfieldWidth / size_renderable;

        var dividers_size = repeats_renderable * DividerCount * 2;

        var new_start =
            Math.Max(Math.Max((int)Camera.Position.Y / size_renderable, 0) * repeats_renderable - dividers_size, 0);
        var new_end = Math.Min(tdw_images.Count,
            (int)(repeats_renderable * (Camera.Position.Y / size_renderable) +
                  (int)(repeats_renderable * ((float)Height / size_renderable) * 1.25)));

        for (var i = new_start; i < new_end; i++)
        {
            var renderable = tdw_images[i];
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

            if (place.X + scale.X < Camera.Position.X || place.X - scale.X > Camera.Position.X + Width) return;
            if (place.Y + scale.Y < Camera.Position.Y || place.Y - scale.Y > Camera.Position.Y + Height) return;

            renderable.Render(Camera);
        }
    }

    private void PlayPlacement(Placement placement)
    {
        var ev = placement.Event;

        var name = ev.SoundEvent;
        var value = ev.Value;

        if (name == null) return;

        if (!ProcessedBuffers.TryGetValue(name, out var value_dictionary)) return;
        if (!value_dictionary.TryGetValue(value, out var buffer)) return;

        buffer.SetVolume((float)(ev.Volume / 100d ?? .5d));

        buffer.PlaySample(AudioContext, remove_current_sample);
        lock (ActiveSamples) ActiveSamples.Add(buffer);

        return;

        void remove_current_sample()
        {
            lock (ActiveSamples) ActiveSamples.Remove(buffer);
        }
    }

    private void CutSounds()
    {
        lock (ActiveSamples)
        {
            foreach (var sample in ActiveSamples)
            {
                sample.Stop();
            }
        }
    }

    private void AudioHandler()
    {
        var current_open_time = _open_time;
        if (OpenAudioHandler) return;
        OpenAudioHandler = true;
        while (Audio_I < _placement.LongLength && !Token.IsCancellationRequested)
        {
            if (current_open_time != _open_time) break;
            
            var placement = _placement[Audio_I];
            if (placement.Index > (ulong)((float)_timing_stopwatch.ElapsedMilliseconds * TimingSampleRate / 1000))
            {
                AudioContext.CheckErrors();
                Thread.Sleep(1);
                continue;
            }

            switch (placement.Event.SoundEvent)
            {
                case "!cut":
                    CutSounds();
                    break;
                
                case "!divider":
                    LastDividerIndex = placement.SequenceIndex;
                    break;
            }

            if ((placement.Event.SoundEvent?.StartsWith('!') ?? true) || placement.Event.SoundEvent is "#!cut")
            {
                Audio_I++;
                continue;
            }

            if (placement.Audible && PlayAudio)
            {
                PlayPlacement(placement);
            }

            Audio_I++;
        }
        OpenAudioHandler = false;
    }

    public void Update()
    {
        if (!FinishedInitializing) return;
        HandleCompositionUpdate();
        Log($"Timing stopwatch: {_timing_stopwatch.Elapsed} / Placement: {_placement.Length} / Audio: {Audio_I} / Video: {Video_I}");
        
        if (Audio_I >= _placement.LongLength && _timing_stopwatch.IsRunning)
        {
            _timing_stopwatch.Stop();
        }
        
        for (; Video_I < _placement.LongLength; Video_I++)
        {
            Camera.Update();
            if (!OpenAudioHandler)
            {
                Task.Run(AudioHandler, Token);
            }

            if (BackingAudio != null)
            {
                BackingAudio.UpdatePlayState(_timing_stopwatch.IsRunning);
                BackingAudio.SyncTime(_timing_stopwatch.Elapsed);
            } 
            
            var placement = _placement[Video_I];

            var element = tdw_images.ElementAtOrDefault((int)placement.SequenceIndex);
            if (element == null) break;

            var position = element.GetPosition() + element.GetTranslation();
            var scale = element.GetScale();

            switch (CameraFollowMode)
            {
                case CameraFollowMode.TDW_Like:
                {
                    float margin = RenderableSize;
                    
                    if (!Camera.IsOutsideOfCameraView(position, scale, margin) && placement.Event.SoundEvent is not "!divider") break;
                    
                    Camera.ScrollTo(new Vector3(0, position.Y - margin, 0f));
                    break;
                }

                case CameraFollowMode.Current_Line:
                {
                    Camera.ScrollTo(position * Vector3.UnitY - Vector3.UnitY * (Height / 2f));
                    break;
                }
            }

            if (placement.Index > (ulong)((float)_timing_stopwatch.ElapsedMilliseconds * TimingSampleRate / 1000)) return;
            
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
                        ValueScale.Add => LastBPM + val,
                        ValueScale.None => val,
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

    private void HandleCompositionUpdate()
    {
        const int update_frequency_ms = 100;
        if (_file_modified_stopwatch.ElapsedMilliseconds < update_frequency_ms) return;
        if (_composition_location == null) return;
        
        var m_date = File.GetLastWriteTime(_composition_location);
        if (m_date.Equals(_composition_date_modified)) return;

        Log("Change detected in composition. Updating.");

        _composition_date_modified = m_date;
        var was_running = _timing_stopwatch.IsRunning;
        
        Manager.RenderBlock.Wait(Token);
        _timing_stopwatch.Stop();
        try
        {
            FileDrop(_composition_location, false);
        }
        finally
        {
            Manager.RenderBlock.Release();
        }
        
        if (Audio_I >= _placement.Length)
        {
            Audio_I = _placement.Length - 1;
            Video_I = Audio_I;
        }
        
        var placement = _placement[Math.Clamp(Audio_I - 1, 0, _placement.Length - 1)];
        var placement_index = placement.Index;
            
        _timing_stopwatch.Seek((long) placement_index * 1000 / TimingSampleRate);
        
        if (was_running) _timing_stopwatch.Start();
        _file_modified_stopwatch.Restart();
    }

    public void Close()
    {
        _timing_stopwatch.Reset();
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
        BackingAudio?.Dispose();

        var decoder = new WaveDecoder();
        var file_stream = File.OpenRead(location);
        var pcm_data = decoder.Read(file_stream);

        var audio = pcm_data.ReadAsFloat32Array(true);

        if (audio == null) return;
        BackingAudio = new BackingAudio(audio, (int) pcm_data.SampleRate);
        
        BackingAudio.PlaySample(AudioContext, () => {});
    }

    private void FileDrop(string? location, bool reset_time)
    {
        if (reset_time)
        {
            Camera = new DollarStoreCamera((0, -300f, 0), (Width, Height));
            _timing_stopwatch.Reset();
            Video_I = Audio_I = 0;
        }
        
        tdw_images.Clear();
        static_objects.Clear();
        start_objects.Clear();

        CutSounds();

        var old_location = _composition_location;
        _composition_location = location;
        Init(Manager);
        
        Resize(Width, Height);
        
        if (old_location != location || reset_time)
            Start();
    }

    public void Input(KeyboardState state)
    {
        const int seek_timeout = 250;
        const int seek_length = 1000;
        
        switch (state.IsKeyPressed(Keys.Space))
        {
            case true when _timing_stopwatch.IsRunning:
                _timing_stopwatch.Stop();
                break;
            
            case true when !_timing_stopwatch.IsRunning:
                _timing_stopwatch.Start();
                break;
        }

        CameraFollowMode = state.IsKeyPressed(Keys.C) switch
        {
            true when CameraFollowMode is CameraFollowMode.Current_Line => CameraFollowMode.TDW_Like,
            true when CameraFollowMode is CameraFollowMode.TDW_Like => CameraFollowMode.Current_Line,
            _ => CameraFollowMode
        };

        var elapsed = _timing_stopwatch.ElapsedMilliseconds;
        if (state.IsKeyDown(Keys.Left) && _seek_stopwatch.ElapsedMilliseconds > seek_timeout)
        {
            _seek_stopwatch.Restart();
            var change = elapsed - seek_length;
            
            var (placement, i) = _placement.Select((placement, i) => (placement , i))
                .MinBy(stack => Math.Abs((long) stack.placement.Index * 1000 / TimingSampleRate - change));
            var placement_index = placement?.Index ?? 0;
            
            _timing_stopwatch.Seek((long) placement_index * 1000 / TimingSampleRate);
            Audio_I = Video_I = i;
        }

        if (state.IsKeyDown(Keys.Right) && _seek_stopwatch.ElapsedMilliseconds > seek_timeout)
        {
            _seek_stopwatch.Restart();
            var change = elapsed + seek_length;
            
            var (placement, i) = _placement.Select((placement, i) => (placement , i))
                .MinBy(stack => Math.Abs((long) stack.placement.Index * 1000 / TimingSampleRate - change));
            var placement_index = placement?.Index ?? 0;
            
            _timing_stopwatch.Seek((long) placement_index * 1000 / TimingSampleRate);
            Audio_I = Video_I = i;
        }

        if (state.IsKeyPressed(Keys.Up))
        {
            _seek_stopwatch.Restart();
            var current_i = Math.Min(Audio_I, _placement.Length - 1);
            int i;

            Placement? placement = null;

            for (i = current_i; i > 0; i--)
            {
                var placement_i = _placement[i];

                if (placement_i.Event.SoundEvent is not "!divider") continue;
                if (placement_i.SequenceIndex == LastDividerIndex) continue;

                placement = placement_i;
                break;
            }

            if (placement == null)
            {
                placement = _placement.First();
                i = 0;
            }
            
            var placement_index = placement.Index;
            
            _timing_stopwatch.Seek((long) placement_index * 1000 / TimingSampleRate);
            Audio_I = Video_I = i;
        }

        if (state.IsKeyPressed(Keys.Down))
        {
            _seek_stopwatch.Restart();
            int i;
            var current_i = Math.Min(Audio_I, _placement.Length - 1);
            
            Placement? placement = null;

            for (i = current_i; i < _placement.LongLength; i++)
            {
                var placement_i = _placement[i];

                if (placement_i.Event.SoundEvent is not "!divider") continue;
                if (placement_i.SequenceIndex == LastDividerIndex) continue;

                placement = placement_i;
                break;
            }

            placement ??= _placement[i - 1];
            var placement_index = placement.Index;
            
            _timing_stopwatch.Seek((long) placement_index * 1000 / TimingSampleRate);
            Audio_I = Video_I = i;
        }

        if (!state.IsKeyPressed(Keys.R)) return;
        FileDrop(_composition_location);
    }
}