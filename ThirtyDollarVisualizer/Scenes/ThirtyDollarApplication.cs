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
using ThirtyDollarVisualizer.Objects.Text;

namespace ThirtyDollarVisualizer.Scenes;

public class ThirtyDollarApplication : IScene
{
    private static readonly List<Renderable> render_objects = new();
    private static readonly List<Renderable> tdw_images = new();
    private readonly Dictionary<string, Dictionary<double, AudibleSample>> ProcessedSamples = new();
    private readonly List<AudibleSample> ActiveSamples = new();
    
    private static Camera Camera = null!;
    private int Height;
    private readonly string? _composition_location;
    private int Width;
    private readonly Stopwatch _timing_stopwatch = new();
    private readonly Action<string> Log = log =>
    {
        Console.WriteLine($"({DateTime.Now:G}): {log}");
    };

    // This is currently a hack, but I can't think of any other way to fix this without restructuring the code.
    private int DividerCount;

    private ColoredPlane _background = null!;
    private ColoredPlane _flash_overlay = null!;
    private ColoredPlane _visible_area = null!;

    private Composition _composition = null!;
    private Placement[] _placement = null!;
    
    private int global_audio_i;
    private int global_video_i;
    
    private float target_y;
    private bool finished_init;
    
    public bool PlayAudio { get; set; }
    public int RenderableSize { get; set; } = 58;
    public int MarginBetweenRenderables { get; set; } = 6;
    
    public int ElementsOnSingleLine = 16;
    public int PlayfieldWidth;

    public ThirtyDollarApplication(int width, int height, string? composition_location)
    {
        Width = width;
        Height = height;
        _composition_location = composition_location;
        
        target_y = -300f;
        var camera_position = new Vector3(0, target_y, 0);
        Camera = new Camera(camera_position, new Vector3(0, 0, 0), Vector3.UnitY, new Vector2i(Width, Height));
    }
    
    public void Init()
    {
        var comp_location = _composition_location ?? throw new Exception("Location is not specified.");
        _composition = Composition.FromString(File.ReadAllText(comp_location));
        
        var calculator = new PlacementCalculator(new EncoderSettings
        {
            SampleRate = 48000
        });

        _placement = calculator.Calculate(_composition).ToArray();

        #region Textures

        Log("Loaded sequence and placement.");
        
        _background = new ColoredPlane(new Vector4(0.21f,0.22f,0.24f, 1f), new Vector3(0, 0, 1f), new Vector2(Width, Height));
        _flash_overlay = new ColoredPlane(new Vector4(1f,1f,1f, 0f), new Vector3(0, 0, 0.75f), new Vector2(Width, Height));

        render_objects.Add(_background);
        render_objects.Add(_flash_overlay);

        PlayfieldWidth = ElementsOnSingleLine * (RenderableSize + MarginBetweenRenderables) + MarginBetweenRenderables + 15 /*px Padding in the site. */;

        var margin_left = (int)((float) Width / 2 - (float) PlayfieldWidth / 2);
        
        var flex_box = new FlexBox(new Vector2i(margin_left + 7,0), 
            new Vector2i(PlayfieldWidth + MarginBetweenRenderables, Height), MarginBetweenRenderables);
        var wh = new Vector2i(RenderableSize,RenderableSize);

        Dictionary<string, Texture> texture_cache = new();
        Dictionary<string, Texture> value_text_cache = new();
        
        _visible_area = new ColoredPlane(new Vector4(0, 0, 0, 0.25f), new Vector3(margin_left,0,0.5f), new Vector2i(PlayfieldWidth, Height));
        render_objects.Add(_visible_area);

        tdw_images.EnsureCapacity(_composition.Events.Length);

        var font_family = Fonts.GetFontFamily();
        var font = font_family.CreateFont(16, FontStyle.Bold);
        
        var volume_font = font_family.CreateFont(11, FontStyle.Bold);
        var volume_color = new Rgba32(204, 204, 204, 1f);

        var i = 0ul;
        Task.Run(UpdateChecker);
        
        foreach (var ev in _composition.Events)
        {
            if (string.IsNullOrEmpty(ev.SoundEvent) || ev.SoundEvent.StartsWith("#"))
            {
                i++;
                continue;
            }
            
            var image = "./Images/" + ev.SoundEvent?.Replace("!", "action_") + ".png";
            
            texture_cache.TryGetValue(image, out var texture);
            if (texture == null)
            {
                texture = new Texture(image);
                texture_cache.Add(image, texture);
            }

            var width_height = new Vector2i(wh.X, wh.Y);
            var aspect_ratio = (float) texture.Width / texture.Height;

            switch (aspect_ratio)
            {
                case > 1:
                    width_height.Y = (int) (width_height.Y / aspect_ratio);
                    break;
                case < 1:
                    width_height.X = (int) (width_height.X * aspect_ratio);
                    break;
            }

            var plane_position = flex_box.AddBox(wh);
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

            if (ev.SoundEvent is "!bg")
            {
                var seconds = ((long) ev.Value >> 24) / 1000f;
                value = seconds.ToString("0.##");
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
                var text_position = new Vector3(plane_position)
                {
                    X = plane_position.X + width_height.X / 2f - value_texture.Width / 2f,
                    Y = plane_position.Y + width_height.Y - value_texture.Height
                };
                text_position.Z -= 0.5f;

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
                    var text_position = new Vector3(plane_position)
                    {
                        X = plane_position.X + width_height.X - volume_texture.Width,
                        Y = plane_position.Y + volume_texture.Height
                    };
                    text_position.Z -= 0.5f;

                    var text = new TexturedPlane(volume_texture, text_position, (volume_texture.Width, volume_texture.Height));
                    plane.Children.Add(text);
                }
            }
            
            #endregion
            
            switch (aspect_ratio)
            {
                case > 1:
                {
                    float marginY = wh.Y - width_height.Y;
                    
                    var position = plane.GetPosition();
                    position.Y += (int) (marginY / 2);
                    
                    plane.SetPosition(position);
                    break;
                }
                case < 1:
                {
                    float marginX = wh.X - width_height.X;
                    
                    var position = plane.GetPosition();
                    position.Y += (int) (marginX / 2);
                    
                    plane.SetPosition(position);
                    break;
                }
            }

            plane.UpdateVertices();
            tdw_images.Add(plane);

            if (ev.SoundEvent is "!divider")
            {
                flex_box.NewLine();
                flex_box.NewLine();
                DividerCount++;
            }
            i++;
        }
        
        Log("Loaded textures.");
        Log("Loading sounds.");
        
        #endregion
        
        Task.Run(async () =>
        {
            await load_audio();
        }).Wait();
        
        Manager.CheckErrors();
        finished_init = true;
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
                
                Console.Write(new string('-', (int) (j - old)));
                await Task.Delay(33);
            }
        }

        async Task load_audio()
        {
            var sample_holder = new SampleHolder();
            sample_holder.DownloadedAllFiles();
            
            await sample_holder.LoadSampleList();
            sample_holder.LoadSamplesIntoMemory();

            var pcm_encoder = new PcmEncoder(sample_holder, new EncoderSettings
            {
                SampleRate = (uint) AudioContext.SampleRate,
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

                if (ProcessedSamples.TryGetValue(name, out var value_dictionary))
                {
                    if (value_dictionary.ContainsKey(value)) continue;
                }

                if (value_dictionary == null)
                {
                    value_dictionary = new Dictionary<double, AudibleSample>();
                    ProcessedSamples.Add(name, value_dictionary);
                }
            
                var sample = new AudibleSample(ev.AudioData);
            
                value_dictionary.Add(value, sample);
            }
        }
    }
    
    public void Resize(int w, int h)
    {
        var resize = new Vector2i(w, h);
        
        Width = w;
        Height = h;
        
        Camera.Viewport = resize;
        GL.Viewport(0,0, w, h);

        if (!finished_init) return;

        var background = _background.GetScale();
        _background.SetScale((w, h, background.Z));

        var flash = _flash_overlay.GetScale();
        _flash_overlay.SetScale((w, h, flash.Z));

        var visible = _visible_area.GetScale();
        _visible_area.SetScale((visible.X, h, visible.Z));
    }

    public void Start()
    {
        _timing_stopwatch.Start();
        Task.Run(AudioHandler);
    }

    public void Render()
    {
        if (!finished_init) return;
        foreach (var renderable in render_objects)
        {
            Manager.CheckErrors();
            renderable.Render(Camera);
        }

        var size_renderable = RenderableSize + MarginBetweenRenderables;
        var repeats_renderable = PlayfieldWidth / size_renderable;

        var dividers_size = repeats_renderable * DividerCount * 2;
        
        var new_start = (int) Math.Max(Math.Max((int) Camera.Position.Y / size_renderable, 0) * repeats_renderable - dividers_size, 0);
        var new_end = Math.Min(tdw_images.Count, 
            (int) (repeats_renderable * (Camera.Position.Y / size_renderable) + 
                   (int) (repeats_renderable * (Height / size_renderable) * 1.25)));

        for (var i = new_start; i < new_end; i++)
        {
            RenderRenderable(i);
        }

        return;

        void RenderRenderable(int index)
        {
            var renderable = tdw_images[index];
            Manager.CheckErrors();

            var position = renderable.GetPosition();
            var scale = renderable.GetScale();
            
            // Bounds checks for viewport.

            if (position.X + scale.X < Camera.Position.X || position.X - scale.X > Camera.Position.X + Width) return;
            if (position.Y + scale.Y < Camera.Position.Y || position.Y - scale.Y > Camera.Position.Y + Height) return;

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
            var delta_y = target_y - current_y;

            scroll_y = delta_y / 120f;
            current_y += scroll_y;
            camera.Position = current_y * Vector3.UnitY;
            
            await Task.Delay(1);
        }

        camera.IsBeingUpdated = false;
    }

    private void PlayPlacement(Placement placement)
    {
        var ev = placement.Event;

        var name = ev.SoundEvent;
        var value = ev.Value;

        if (name == null) return;

        if (!ProcessedSamples.TryGetValue(name, out var value_dictionary)) return;
        if (!value_dictionary.TryGetValue(value, out var sample)) return;
        
        sample.SetVolume((float) (ev.Volume / 100d ?? .5d));
        lock (ActiveSamples) ActiveSamples.Add(sample);
        
        var task = Task.Factory.StartNew(sample.PlaySample).ContinueWith(_ =>
        {
            lock (ActiveSamples) ActiveSamples.Remove(sample);
        });

        Task.Run(async () => await task);
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
        while (global_audio_i < _placement.LongLength)
        {
            var placement = _placement[global_audio_i];
            if (placement.Index > (ulong) ((float)_timing_stopwatch.ElapsedMilliseconds * 48000 / 1000))
            {
                continue;
            }

            if ((placement.Event.SoundEvent?.StartsWith('!') ?? true) || placement.Event.SoundEvent is "#!cut")
            {
                global_audio_i++;
                continue;
            }
            
            if (placement.Audible && PlayAudio)
            {
                PlayPlacement(placement);
            }

            global_audio_i++;
        }
    }

    public void Update()
    {
        if (!finished_init) return;
        for (;global_video_i < _placement.LongLength; global_video_i++)
        {
            var placement = _placement[global_video_i];
            
            var renderable = tdw_images.ElementAtOrDefault((int) placement.SequenceIndex);
            if (renderable == null) break;

            var position = renderable.GetPosition();
            var scale = renderable.GetScale();
            target_y = position.Y + scale.Y - Height / 2f;

            if (placement.Index > (ulong) ((float)_timing_stopwatch.ElapsedMilliseconds * 48000 / 1000)) break;

            new Task(Bounce).Start();
            if (placement.Event.SoundEvent?.StartsWith('!') ?? false)
            {
                ColorTools.Fade(renderable);
            }
            
            switch (placement.Event.SoundEvent)
            {
                case "!cut":
                    CutSounds();
                    break;
                
                case "!bg":
                {
                    var parsed_value = (long) placement.Event.Value;
                
                    var r = (byte) parsed_value;
                    var g = (byte) (parsed_value >> 8);
                    var b = (byte) (parsed_value >> 16);
                    var color = new Vector4(r / 255f, b / 255f, g / 255f, 1f);

                    var seconds = (parsed_value >> 24) / 1000f;

                    ColorTools.ChangeColor(_background, color, seconds).GetAwaiter();
                    break;
                }

                case "!flash":
                {
                    Task.Run(async () =>
                    {
                        await ColorTools.ChangeColor(_flash_overlay, new Vector4(1, 1, 1, 1), 0.125f);
                        await ColorTools.ChangeColor(_flash_overlay, new Vector4(0,0,0,0), 0.25f);
                    });
                    break;
                }
                
                // TODO: implement behavior for !pulse
            }

            SmoothScrollCamera(Camera).GetAwaiter();
            continue;

            async void Bounce()
            {
                //Log($"Bouncing: {placement.Event}");
                for (var i = 0d; i < 240; i++)
                {
                    var factor = (float) Math.Sin(Math.PI * (i / 240));
                    renderable.SetOffset(Vector3.UnitY * factor * 20);
                    await Task.Delay(2);
                }
            }
        }
    }
}