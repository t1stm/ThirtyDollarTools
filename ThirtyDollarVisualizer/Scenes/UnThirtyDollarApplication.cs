using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarVisualizer.Audio;
using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.Scenes;

public class UnThirtyDollarApplication : IScene
{
    public readonly SequencePlayer SequencePlayer = new();
    private DollarStoreCamera Camera = new DollarStoreCamera(Vector3.Zero, (1920, 1080));
    private readonly List<Renderable> static_objects = new();
    private Manager Manager = null!;

    private int Width;
    private int Height;

    public void Init(Manager manager)
    {
        Manager = manager;
        Camera = new DollarStoreCamera(Vector3.Zero, (Width, Height));
    }

    public void Start()
    {
    }

    public void Render()
    {
    }

    public void Update()
    {
    }

    public void Resize(int w, int h)
    {
        Width = w;
        Height = h;
        Camera = new DollarStoreCamera(Vector3.Zero, (Width, Height));
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