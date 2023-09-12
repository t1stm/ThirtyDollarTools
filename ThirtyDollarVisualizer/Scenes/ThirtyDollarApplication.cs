using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SixLabors.Fonts;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Resamplers;
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
    private static readonly List<Renderable> start_objects = new();
    private static readonly List<Renderable> static_objects = new();
    private static readonly List<Renderable> tdw_images = new();
    private readonly Stopwatch _timing_stopwatch = new();
    private int Video_I;

    private readonly AudioContext AudioContext = new();
    private readonly Dictionary<string, Dictionary<double, AudibleBuffer>> ProcessedBuffers = new();
    private readonly List<AudibleBuffer> ActiveSamples = new();
    private int Audio_I;

    private static Camera Camera = null!;
    private int Width;
    private int Height;
    private float TargetY;
    private int PlayfieldWidth;

    private ColoredPlane _background = null!;
    private ColoredPlane _flash_overlay = null!;
    private ColoredPlane _visible_area = null!;

    private Composition _composition = null!;
    private readonly string? _composition_location;
    private Placement[] _placement = null!;
    private const int TimingSampleRate = 100_000;

    private CancellationToken Token => TokenSource.Token;
    private readonly CancellationTokenSource TokenSource = new();
    private Manager? Manager;

    // This is currently a hack, but I can't think of any other way to fix this without restructuring the code.
    private int DividerCount;
    private bool FinishedInitializing;
    private bool CancelAllAnimations;
    private int LeftMargin;
    
    public bool PlayAudio { get; init; }
    public SampleHolder? SampleHolder { get; set; }
    public int RenderableSize { get; init; } = 64;
    public int MarginBetweenRenderables { get; init; } = 6;
    public int ElementsOnSingleLine { get; init; } = 16;
    public CameraFollowMode CameraFollowMode { get; init; } = CameraFollowMode.TDW_Like;
    public string? BackgroundVertexShaderLocation { get; init; }
    public string? BackgroundFragmentShaderLocation { get; init; }
    public Action<string> Log { get; init; } = log => { Console.WriteLine($"({DateTime.Now:G}): {log}"); };

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

        TargetY = -300f;
        var camera_position = new Vector3(0,TargetY,0);
        Camera = new Camera(camera_position, -Vector3.UnitZ, Vector3.UnitY, new Vector2i(Width, Height));
    }

    /// <summary>
    /// This method loads the composition, textures and sounds.
    /// </summary>
    /// <exception cref="Exception">Exception thrown when one of the arguments is invalid.</exception>
    public void Init(Manager manager)
    {
        Manager = manager;
        var comp_location = _composition_location ?? throw new Exception("Location is not specified.");
        _composition = Composition.FromString(File.ReadAllText(comp_location));

        var calculator = new PlacementCalculator(new EncoderSettings
        {
            SampleRate = TimingSampleRate
        });

        _placement = calculator.Calculate(_composition).ToArray();

        #region Textures

        Log("Loaded sequence and placement.");

        Shader? optional_shader = null;
        if (BackgroundVertexShaderLocation != null && BackgroundFragmentShaderLocation != null)
        {
            optional_shader = new Shader(BackgroundVertexShaderLocation, BackgroundFragmentShaderLocation);
        }

        _background = new ColoredPlane(new Vector4(0.21f, 0.22f, 0.24f, 1f), new Vector3(0, 0, 1f),
            new Vector2(Width, Height),
            optional_shader);
        
        _flash_overlay = new ColoredPlane(new Vector4(1f, 1f, 1f, 0f), new Vector3(0, 0, 0.75f),
            new Vector2(Width, Height));

        static_objects.Add(_background);
        static_objects.Add(_flash_overlay);

        PlayfieldWidth = ElementsOnSingleLine * (RenderableSize + MarginBetweenRenderables) + MarginBetweenRenderables +
                         15 /*px Padding in the site. */;

        LeftMargin = (int)((float)Width / 2 - (float)PlayfieldWidth / 2);

        var flex_box = new FlexBox(new Vector2i(LeftMargin + 7, 0),
            new Vector2i(PlayfieldWidth + MarginBetweenRenderables, Height), MarginBetweenRenderables);
        var wh = new Vector2i(RenderableSize, RenderableSize);

        Dictionary<string, Texture> texture_cache = new();
        Dictionary<string, Texture> value_text_cache = new();

        _visible_area = new ColoredPlane(new Vector4(0, 0, 0, 0.25f), new Vector3(LeftMargin, 0, 0.5f),
            new Vector2i(PlayfieldWidth, Height));
        static_objects.Add(_visible_area);

        tdw_images.EnsureCapacity(_composition.Events.Length);
        
        var font_family = Fonts.GetFontFamily();
        var greeting_font = font_family.CreateFont(36, FontStyle.Bold);

        var greeting_texture = new Texture(greeting_font, "DON'T LECTURE ME WITH YOUR THIRTY DOLLAR VISUALIZER");
        var moai = new Texture("ThirtyDollarVisualizer.Assets.Textures.moai.png");
        
        start_objects.Add(new TexturedPlane(greeting_texture, new Vector3(Width / 2f - greeting_texture.Width / 2f, -200,0.25f), 
            (greeting_texture.Width, greeting_texture.Height)));
        
        start_objects.Add(new TexturedPlane(moai, new Vector3(Width / 2f - greeting_texture.Width / 2f - greeting_texture.Height, -200, 0.25f), 
            new Vector2(greeting_texture.Height,greeting_texture.Height)));
        
        start_objects.Add(new TexturedPlane(moai, new Vector3(Width / 2f + greeting_texture.Width / 2f, -200, 0.25f), 
            new Vector2(greeting_texture.Height,greeting_texture.Height)));
        
        var font = font_family.CreateFont(16, FontStyle.Bold);

        var volume_font = font_family.CreateFont(11, FontStyle.Bold);
        var volume_color = new Rgba32(204, 204, 204, 1f);

        var i = 0ul;
        Task.Run(UpdateChecker, Token);

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
                    await sample_holder.DownloadFiles();
                }
                sample_holder.LoadSamplesIntoMemory();
            }
        }, Token).Wait(Token);

        foreach (var ev in _composition.Events)
        {
            if (string.IsNullOrEmpty(ev.SoundEvent) || ev.SoundEvent.StartsWith("#"))
            {
                i++;
                continue;
            }

            CreateEventRenderable(ev, texture_cache, wh, flex_box, value_text_cache, font, volume_color, volume_font);
            i++;
        }

        Log("Loaded textures.");
        Log("Loading sounds.");

        #endregion

        Task.Run(async () => { await LoadAudio(); }, Token).Wait(Token);

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
    /// <param name="value_text_cache">The cache for the textures of the value and the volume.</param>
    /// <param name="font">The font you want to use for the generated textures.</param>
    /// <param name="volume_color">The color of the volume font.</param>
    /// <param name="volume_font">The volume font.</param>
    /// <exception cref="Exception"></exception>
    private void CreateEventRenderable(Event ev, IDictionary<string, Texture> texture_cache, Vector2i wh,
        FlexBox flex_box,
        IDictionary<string, Texture> value_text_cache, Font font, Rgba32 volume_color, Font volume_font)
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

        var plane = new TexturedPlane(texture, plane_position, width_height);

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
                Y = box_position.Y + RenderableSize + MarginBetweenRenderables - value_texture.Height,
                Z = box_position.Z
            };
            text_position.Z -= 0.1f;

            var text = new TexturedPlane(value_texture, text_position, (value_texture.Width, value_texture.Height));
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
                value_text_cache.TryGetValue(volume_text, out volume_texture);
                if (volume_texture == null)
                {
                    volume_texture = new Texture(volume_font, volume_text);
                    value_text_cache.Add(volume_text, volume_texture);
                }
            }

            if (volume_texture != null)
            {
                var text_position = new Vector3
                {
                    X = box_position.X + width_height.X - volume_texture.Width / 2f,
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


        plane.SetVertices();
        tdw_images.Add(plane);

        if (ev.SoundEvent is not "!divider") return;

        flex_box.NewLine();
        flex_box.NewLine();
        DividerCount++;
    }

    private async Task LoadAudio()
    {
        var pcm_encoder = new PcmEncoder(SampleHolder ?? throw new Exception("Sample holder is null."), new EncoderSettings
        {
            SampleRate = (uint)AudioContext.SampleRate,
            Channels = 2,
            CutDelayMs = 250,
            Resampler = new LinearResampler()
        }, Log);

        var (processed_samples, _) = await pcm_encoder.GetAudioSamples(-1, _placement, CancellationToken.None);

        AudioContext.Create(processed_samples.Count * 2);
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
        CancelAllAnimations = true;

        Thread.Sleep(33);
        var resize = new Vector2i(w, h);

        Width = w;
        Height = h;

        Camera.Viewport = resize;
        GL.Viewport(0, 0, w, h);

        if (!FinishedInitializing) return;

        var background = _background.GetScale();
        _background.SetScale((w, h, background.Z));

        var flash = _flash_overlay.GetScale();
        _flash_overlay.SetScale((w, h, flash.Z));

        var visible = _visible_area.GetScale();
        _visible_area.SetScale((visible.X, h, visible.Z));

        var visible_position = _visible_area.GetPosition();
        visible_position.X = Width / 2f - visible.X / 2;
        _visible_area.SetPosition(visible_position);

        var current_margin = visible_position.X;
        
        foreach (var image in tdw_images)
        {
            var original_offset = image.GetTranslation();
            var new_offset = new Vector3(original_offset)
            {
                X = current_margin - LeftMargin
            };

            image.SetTranslation(new_offset);
        }
        
        CancelAllAnimations = false;
    }

    public void Start()
    {
        Task.Run(async () =>
        {
            await Task.Delay(3000, Token);
            
            _timing_stopwatch.Start();
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
                Y = Camera.Position.Y
            };

            renderable.SetTranslation(new_position);
            RenderRenderable(renderable);
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

    private async Task SmoothScrollCamera(Camera camera)
    {
        if (camera.IsBeingUpdated) return;
        camera.IsBeingUpdated = true;

        var scroll_y = 1f;

        while (Math.Abs(scroll_y) > 0.05f)
        {
            var current_y = camera.Position.Y;
            var delta_y = TargetY - current_y;

            scroll_y = delta_y / 120f;
            current_y += scroll_y;
            camera.Position = current_y * Vector3.UnitY;

            await Task.Delay(1, Token);
        }

        camera.IsBeingUpdated = false;
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
        while (Audio_I < _placement.LongLength && !Token.IsCancellationRequested)
        {
            var placement = _placement[Audio_I];
            if (placement.Index > (ulong)((float)_timing_stopwatch.ElapsedMilliseconds * TimingSampleRate / 1000))
            {
                AudioContext.CheckErrors();
                Thread.Sleep(1);
                continue;
            }

            if (placement.Event.SoundEvent is "!cut")
            {
                CutSounds();
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
    }

    public void Update()
    {
        if (!FinishedInitializing) return;
        for (; Video_I < _placement.LongLength; Video_I++)
        {
            var placement = _placement[Video_I];

            var renderable = tdw_images.ElementAtOrDefault((int)placement.SequenceIndex);
            if (renderable == null) break;

            var position = renderable.GetPosition() + renderable.GetTranslation();
            var scale = renderable.GetScale();

            var current_Y = position.Y + scale.Y;

            switch (CameraFollowMode)
            {
                case CameraFollowMode.TDW_Like:
                {
                    if (current_Y > Camera.Position.Y + 5 * (Height / 6f) || current_Y < Camera.Position.Y)
                    {
                        TargetY = current_Y - Height / 6f;
                    }
                    break;
                }

                case CameraFollowMode.Current_Line:
                {
                    TargetY = current_Y - Height / 2f;
                    break;
                }
            }

            if (placement.Index > (ulong)((float)_timing_stopwatch.ElapsedMilliseconds * TimingSampleRate / 1000)) return;
            
            if (placement.Event.SoundEvent?.StartsWith('!') ?? false)
            {
                Task.Run(() => ColorTools.Fade(renderable), Token);
                Task.Run(() => ExpandReturn(renderable), Token);
            }
            else if (placement.Event.SoundEvent is not "#!cut")
            {
                Task.Run(() => Bounce(renderable), Token);
            }

            switch (placement.Event.SoundEvent)
            {
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

                // TODO: implement behavior for !pulse
            }

            Task.Run(async () => await SmoothScrollCamera(Camera), Token);
        }

        if (Video_I > _placement.LongLength)
        {
            Manager?.Close();
        }
    }

    private async void ExpandReturn(Renderable renderable)
    {
        var original_scale = renderable.GetScale();
        var original_position = renderable.GetPosition();
        
        for (var i = 0d; i < 240; i++)
        {
            if (CancelAllAnimations)
            {
                renderable.SetScale(original_scale);
                renderable.SetPosition(original_position);
                return;
            }
            
            var factor = (float) Math.Sin(Math.PI * (i / 240));
            var scale_factor = 1 + factor * .10f;

            var new_scale = original_scale * scale_factor;
            var new_translation = new Vector3(original_position);
            new_translation.X -= factor * 0.05f * original_scale.X;
            new_translation.Y -= factor * 0.05f * original_scale.Y;
            
            renderable.SetScale(new_scale);
            renderable.SetPosition(new_translation);
            
            await Task.Delay(1, Token);
        }
        
        renderable.SetScale(original_scale);
        renderable.SetPosition(original_position);
    }
    
    private async void Bounce(Renderable renderable)
    {
        var original_offset = renderable.GetTranslation();
                
        for (var i = 0d; i < 240; i++)
        {
            if (CancelAllAnimations)
            {
                renderable.SetTranslation(original_offset);
                await Task.Delay(2, Token);
                        
                original_offset = renderable.GetTranslation();
                continue;
            }
            var factor = (float)Math.Sin(Math.PI * (i / 240));
            var new_position = new Vector3(original_offset)
            {
                Y = - factor * 20
            };

            renderable.SetTranslation(new_position);
            await Task.Delay(2, Token);
        }

        original_offset.Y = 0;
        renderable.SetTranslation(original_offset);
    }

    public void Close()
    {
        _timing_stopwatch.Stop();
    }
}