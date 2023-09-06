using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Resamplers;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Audio;
using ThirtyDollarVisualizer.Helpers.Positioning;
using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Objects.Planes;
using ErrorCode = OpenTK.Graphics.OpenGL.ErrorCode;

namespace ThirtyDollarVisualizer;

public class Manager : GameWindow
{
    private static readonly List<Renderable> render_objects = new();
    private static readonly List<Renderable> tdw_images = new();
    private Dictionary<string, Dictionary<double, AudibleSample>> ProcessedSamples = new();
    
    private static Camera Camera = null!;
    private readonly int Height;
    private readonly string? _composition_location;
    private readonly int Width;
    private readonly Stopwatch _timing_stopwatch = new();
    private readonly Action<string> Log = Console.WriteLine;
    private readonly Action LogClear = Console.Clear;

    private ColoredPlane _background = null!;
    private ColoredPlane _visible_area = null!;

    private Composition _composition = null!;
    private Placement[] _placement = null!;

    private int global_i;
    private float y_position;
    private bool updating_position;
    private bool updating_background;

    public Manager(int width, int height, string title, string? composition_location = null) : base(new GameWindowSettings
        {
            UpdateFrequency = 240
        },
        new NativeWindowSettings { Size = (width, height), Title = title })
    {
        Width = width;
        Height = height;
        _composition_location = composition_location;
    }

    private static void CheckErrors()
    {
        ErrorCode errorCode;
        while ((errorCode = GL.GetError()) != ErrorCode.NoError)
            Console.WriteLine($"[OpenGL Error]: (0x{(int)errorCode:x8}) \'{errorCode}\'");
    }

    protected override void OnLoad()
    {
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        
        CheckErrors();

        var comp_location = _composition_location ?? 
                            "/home/kris/RiderProjects/ThirtyDollarWebsiteConverter/ThirtyDollarDebugApp/Included Sequences/(cyvos) Avicii - Sunset Jesus.🗿";
        _composition = Composition.FromString(File.ReadAllText(comp_location));
        
        var calculator = new PlacementCalculator(new EncoderSettings
        {
            SampleRate = 48000
        });

        _placement = calculator.Calculate(_composition).ToArray();

        #region Textures
        
        y_position = -300f;
        var camera_position = new Vector3(0, y_position, 0);

        Log("Loaded sequence and placement.");
        
        _background = new ColoredPlane(new Vector4(0.21f,0.22f,0.24f, 1f), new Vector3(0, 0, 1f), new Vector2(Width, Height));
        _visible_area = new ColoredPlane(new Vector4(0, 0, 0, 0.25f), new Vector3(375,0,0.5f), new Vector2i(1165, Height));
        
        render_objects.Add(_background);
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

            tdw_images.Add(new TexturedPlane(texture, flex_box.AddBox(wh), wh));

            if (ev.SoundEvent is "!divider")
            {
                flex_box.NewLine();
                flex_box.NewLine();
            }
            i++;
        }

        Camera = new Camera(camera_position, new Vector3(0, 0, 0), Vector3.UnitY, new Vector2i(Width, Height));
        
        Log("Loaded textures.");
        Log("Loading sounds");
        
        #endregion

        Task.Run(async () =>
        {
            await load_audio();
        }).Wait();
        
        CheckErrors();
        base.OnLoad();

        new Task(waiter).Start();
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
            
            AudioContext.Create(processed_samples.Count + 16);
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

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        var resize = new Vector2i(e.Width, e.Height);

        Camera.Viewport = resize;
        GL.Viewport(0,0, e.Width, e.Height);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    { 
        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.ClearColor(.0f, .0f, .0f, 1f);

        foreach (var renderable in render_objects)
        {
            CheckErrors();
            renderable.Render(Camera);
        }
        
        foreach (var renderable in tdw_images)
        {
            CheckErrors();

            var position = renderable.GetPosition();
            var scale = renderable.GetScale();
            
            // Bounds checks for viewport.

            if (position.X + scale.X < Camera.Position.X || position.X - scale.X > Camera.Position.X + Width) continue;
            if (position.Y + scale.Y < Camera.Position.Y || position.Y - scale.Y > Camera.Position.Y + Height) continue;

            renderable.Render(Camera);
        }

        SwapBuffers();
    }

    private async void SmoothScrollCamera()
    {
        if (updating_position) return;
        updating_position = true;

        var scroll_y = 1f;
        
        while (Math.Abs(scroll_y) > 0.05f)
        {
            var current_y = Camera.Position.Y;
            var delta_y = y_position - current_y;

            scroll_y = delta_y / 120f;
            current_y += scroll_y;
            Camera.Position = current_y * Vector3.UnitY;
            
            await Task.Delay(1);
        }

        updating_position = false;
    }

    private static async void Fade(Renderable renderable)
    {
        const float max_decrease = 100f;
        const float steps = 100;
        
        for (var i = 0f; i < steps; i++)
        {
            var color = new Vector4(0,0,0, Math.Max(i * (max_decrease / steps) / 255f, 0f));
            
            renderable.SetColor(color);
            await Task.Delay(1);
        }
    }

    private async void ChangeBackground(Vector4 color, float seconds)
    {
        if (updating_background)
        {
            updating_background = false;
            await Task.Delay(34);
            ChangeBackground(color, seconds);
            return;
        }
        
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        var old_color = _background.GetColor();

        updating_background = true;
        float elapsed;
        while ((elapsed = stopwatch.ElapsedMilliseconds / 1000f) < seconds && updating_background)
        {
            var delta = (float) Math.Clamp(elapsed / seconds, 0.01, 1);
            
            _background.SetColor(old_color + color * delta);
            await Task.Delay(33);
        }

        updating_background = false;
        stopwatch.Stop();
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
        sample.PlaySample();
    }

    private void CutSounds()
    {
        
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        while (global_i < _placement.LongLength)
        {
            var placement = _placement[global_i];
            
            var renderable = tdw_images.ElementAtOrDefault((int) placement.SequenceIndex);
            if (renderable == null) break;

            var position = renderable.GetPosition();
            y_position = position.Y - Height / 2f;

            if (placement.Index > (ulong) ((float)_timing_stopwatch.ElapsedMilliseconds * 48000 / 1000)) break;
            
            if (placement.Audible)
            {
                PlayPlacement(placement);
            }

            new Task(Bounce).Start();
            if (placement.Event.SoundEvent?.StartsWith('!') ?? false)
            {
                new Task(() => Fade(renderable)).Start();
            }

            if (placement.Event.SoundEvent is "!bg")
            {
                var parsed_value = (long)placement.Event.Value;
                
                var r = (byte) parsed_value;
                var g = (byte) (parsed_value >> 8);
                var b = (byte) (parsed_value >> 16);
                var color = new Vector4(r / 256f, b / 256f, g / 256f, 1f);

                var seconds = (parsed_value >> 24) / 1000f;
                
                new Task(() => ChangeBackground(color, seconds)).Start();
            }
            
            // TODO: implement behavior for !pulse
            // TODO: add audio output with OpenAL
            
            new Task(SmoothScrollCamera).Start();
            
            global_i++;
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

        if (global_i >= _placement.LongLength)
        {
            Log("Reached the end of composition.");
        }
    }

    public override void Close()
    {
        AudioContext.Destroy();
        base.Close();
    }
}