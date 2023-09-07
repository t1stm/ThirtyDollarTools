using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Resamplers;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Audio;
using ThirtyDollarVisualizer.Helpers.Color;
using ThirtyDollarVisualizer.Helpers.Positioning;
using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Objects.Planes;

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
    private readonly Action<string> Log = Console.WriteLine;
    private readonly Action LogClear = Console.Clear;

    private ColoredPlane _background = null!;
    private ColoredPlane _flash_overlay = null!;
    private ColoredPlane _visible_area = null!;

    private Composition _composition = null!;
    private Placement[] _placement = null!;
    
    private int global_audio_i;
    private int global_video_i;
    private float target_y;

    private bool finished_init; 

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
        _visible_area = new ColoredPlane(new Vector4(0, 0, 0, 0.25f), new Vector3(375,0,0.5f), new Vector2i(1165, Height));
        
        render_objects.Add(_background);
        render_objects.Add(_flash_overlay);
        render_objects.Add(_visible_area);
        
        var flex_box = new FlexBox(new Vector2i(375,0), new Vector2i(1600, Height), 10);
        var wh = new Vector2i(86,86);

        Dictionary<string, Texture> texture_cache = new();

        var i = 0ul;
        new Task(UpdateChecker).Start();

        tdw_images.EnsureCapacity(_composition.Events.Length);

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
            var aspect_ratio = (float)texture.Width / texture.Height;

            switch (aspect_ratio)
            {
                case > 1:
                    width_height.Y = (int) (width_height.Y / aspect_ratio);
                    break;
                case < 1:
                    width_height.X = (int) (width_height.X * aspect_ratio);
                    break;
            }

            var plane = new TexturedPlane(texture, flex_box.AddBox(wh), width_height);
            
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

        new Task(waiter).Start();
        finished_init = true;
        return;

        async void UpdateChecker()
        {
            var len = (ulong)_composition.Events.LongLength;
            var old = 0ul;

            while (i < len)
            {
                if (i - old > 64)
                {
                    Console.Clear();
                    Console.WriteLine($"({i}) - ({_composition.Events.LongLength}) ");
                    old = i;
                }
                
                Console.Write(new string('-', (int) (i - old)));
                await Task.Delay(33);
            }
        }

        async void waiter()
        {
            LogClear();
            Log("Finished preparing.");

            for (var j = 0; j < 3; j++)
            {
                Console.WriteLine(3 - j);
                await Task.Delay(1000);
            }
            
            _timing_stopwatch.Start();
            
            AudioHandler();
            Log("Starting stopwatch.");
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

    public void Start()
    {
        
    }

    public void Render()
    {
        if (!finished_init) return;
        foreach (var renderable in render_objects)
        {
            Manager.CheckErrors();
            renderable.Render(Camera);
        }
        
        foreach (var renderable in tdw_images)
        {
            Manager.CheckErrors();

            var position = renderable.GetPosition();
            var scale = renderable.GetScale();
            
            // Bounds checks for viewport.

            if (position.X + scale.X < Camera.Position.X || position.X - scale.X > Camera.Position.X + Width) continue;
            if (position.Y + scale.Y < Camera.Position.Y || position.Y - scale.Y > Camera.Position.Y + Height) continue;

            renderable.Render(Camera);
        }
    }

    public void Update()
    {
        if (!finished_init) return;
        while (global_video_i < _placement.LongLength)
        {
            var placement = _placement[global_video_i];
            
            var renderable = tdw_images.ElementAtOrDefault((int) placement.SequenceIndex);
            if (renderable == null) break;

            var position = renderable.GetPosition();
            target_y = position.Y - Height / 2f;

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
            
            global_video_i++;
            continue;

            async void Bounce()
            {
                Log($"Bouncing: {placement.Event}");
                for (var i = 0d; i < 240; i++)
                {
                    var scale = (float) Math.Sin(Math.PI * (i / 240));
                    renderable.SetOffset(Vector3.UnitY * scale * 20);
                    await Task.Delay(2);
                }
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

    private async void AudioHandler()
    {
        while (global_audio_i < _placement.LongLength)
        {
            var placement = _placement[global_audio_i];
            if (placement.Index > (ulong) ((float)_timing_stopwatch.ElapsedMilliseconds * 48000 / 1000))
            {
                await Task.Delay(1);
                continue;
            }

            if ((placement.Event.SoundEvent?.StartsWith('!') ?? true) || placement.Event.SoundEvent is "#!cut")
            {
                global_audio_i++;
                continue;
            }
            
            if (placement.Audible)
            {
                PlayPlacement(placement);
            }

            global_audio_i++;
        }
    }
}