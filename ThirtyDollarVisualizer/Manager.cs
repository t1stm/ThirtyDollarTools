using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer;

public class Manager : GameWindow
{
    private readonly int Width;
    private readonly int Height;
    
    private static readonly List<Renderable> render_objects = new();
    private static Camera Camera = null!;

    public Manager(int width, int height, string title) : base(GameWindowSettings.Default,
        new NativeWindowSettings { Size = (width, height), Title = title })
    {
        Width = width;
        Height = height;
    }

    private static void ClearAllErrors()
    {
        while (GL.GetError() != ErrorCode.NoError)
        {
        }
    }

    private static void CheckErrors()
    {
        ErrorCode errorCode;
        while ((errorCode = GL.GetError()) != ErrorCode.NoError)
            Console.WriteLine($"[OpenGL Error]: (0x{(int)errorCode:x8}) \'{errorCode}\'");
    }

    protected override void OnLoad()
    {
        CheckErrors();
        var plane_size = new Vector2(256f, 256f);
        
        var plane = new ColoredPlane( new Vector4(1, 0, 0, 1), plane_size);
        render_objects.Add(plane);

        Camera = new Camera(new Vector3(0, 0, 0), new Vector3(0, 0, 0), Vector3.UnitY, Width, Height);
        
        CheckErrors();
        base.OnLoad();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        
        Camera.Width = e.Width;
        Camera.Height = e.Height;
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.ClearColor(.0f, .0f, .0f,1f);

        foreach (var renderable in render_objects)
        {
            CheckErrors();
            renderable.Render(Camera);
        }
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        
    }
}