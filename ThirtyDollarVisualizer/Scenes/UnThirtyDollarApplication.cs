using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.Fonts;
using ThirtyDollarConverter.Objects;
using ThirtyDollarVisualizer.Audio;
using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Objects.Text;

namespace ThirtyDollarVisualizer.Scenes;

public class UnThirtyDollarApplication : ThirtyDollarWorkflow, IScene
{
    private DollarStoreCamera Camera;
    private readonly List<Renderable> static_objects = new();
    private List<MidiKey> key_objects = new();
    private Manager Manager = null!;
    private DynamicText _dynamic_text = null!;
    private Stopwatch _open_stopwatch = new();

    private int Width;
    private int Height;
    private ColoredPlane _background = null!;

    public UnThirtyDollarApplication(int width, int height, AudioContext? audio_context)
    {
        Width = width;
        Height = height;
        Camera = new DollarStoreCamera(Vector3.Zero, (Width, Height));
    }

    public void Init(Manager manager)
    {
        Manager = manager;
        Manager.RenderBlock.Wait();
        _dynamic_text = new DynamicText
        {
            FontStyle = FontStyle.Bold,
            FontSizePx = 36f,
            Value = "New init."
        };
        _dynamic_text.SetPosition((Width / 2f, Height / 2f, 0f), PositionAlign.Center);
        _open_stopwatch.Restart();

        _background = new ColoredPlane(new Vector4(0.21f, 0.22f, 0.24f, 1f), 
            new Vector3(0,0, 0f),
            new Vector2(Width, Height));
        _background.UpdateModel(false);
        
        Manager.RenderBlock.Release();
        SetPianoKeys();

        Task.Run(async () =>
        {
            await Task.Delay(1000);
            while (Manager.Exists)
                for (var i = 0; i < key_objects.Count; i++)
                {
                    Console.WriteLine($"[UnThirtyDollarApplication] Pressing piano key {i}");
                    var keys = key_objects[i];
                    keys.Press(333);
                    await Task.Delay(250);
                }
        });
    }

    private void SetPianoKeys(int min_v = -4, int max_v = 4)
    {
        Manager.RenderBlock.Wait();

        var delta = Math.Abs(min_v) + max_v * 1f;
        var temp_width_single = Width / delta;
        var temp_width = Width - temp_width_single;
        var width_single = temp_width / delta;
        var height = 200;
        var position_y = Height - height;

        var renderables = new List<MidiKey>();
        
        float w = 0;
        for (var i = min_v; i <= max_v; i++)
        {
            var plane = new MidiKey((0.1f, 0.1f, 0.1f, 1f), (w, position_y, 0), (width_single, height))
            {
                BorderColor = (1f, 0f, 0f, 1f),
                BorderSizePx = 2f
            };
            
            renderables.Add(plane);
            plane.UpdateModel(false);

            var static_text = new StaticText
            {
                FontStyle = FontStyle.Bold,
                FontSizePx = 16f,
                Value = $"{i}"
            }.WithPosition((w + width_single / 2f, position_y + 10f,0), PositionAlign.TopCenter);
            plane.Children.Add(static_text);
            
            w += width_single;
        }

        key_objects = renderables;
        Manager.RenderBlock.Release();
    }
    
    protected override void HandleAfterSequenceUpdate(TimedEvents events)
    {
    }

    protected override void SetSequencePlayerSubscriptions(SequencePlayer player)
    {
    }

    public void Start()
    {
    }

    public void Render()
    {
        Manager.CheckErrors();
        _background.Render(Camera);
        _dynamic_text.Render(Camera);

        foreach (var renderable in key_objects)
        {
            renderable.Render(Camera);
        }
    }

    public void Update()
    {
        _dynamic_text.SetTextContents($"Test {_open_stopwatch.Elapsed}");
    }

    public void Resize(int w, int h)
    {
        Width = w;
        Height = h;
        Camera = new DollarStoreCamera(Vector3.Zero, (Width, Height));
        _background.SetScale((Width,Height,1f));
        GL.Viewport(0,0, Width, Height);
        SetPianoKeys();
    }

    public void Close()
    {
    }

    public void FileDrop(string location)
    {
    }

    public void Keyboard(KeyboardState state)
    {
    }

    public void Mouse(MouseState state)
    {
    }
}