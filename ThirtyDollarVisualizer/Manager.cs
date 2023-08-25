using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Objects.Planes;
using ErrorCode = OpenTK.Graphics.OpenGL.ErrorCode;

namespace ThirtyDollarVisualizer;

public class Manager : GameWindow
{
    private static readonly List<Renderable> render_objects = new();
    private static Camera Camera = null!;
    private readonly int Height;
    private readonly int Width;

    public Manager(int width, int height, string title) : base(GameWindowSettings.Default,
        new NativeWindowSettings { Size = (width, height), Title = title })
    {
        Width = width;
        Height = height;
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
        render_objects.Add(new ColoredPlane(new Vector4(1, 0, 0, 1), 
            new Vector2(100f, 100f), new Vector2(256f, 256f)));
        render_objects.Add(new ColoredPlane(new Vector4(0, 1, 0, 1), 
            new Vector2(400f, 100f), new Vector2(256f, 256f)));
        render_objects.Add(new ColoredPlane(new Vector4(0, 0, 1, 1), 
            new Vector2(100f, 400f), new Vector2(256f, 256f)));
        

        Camera = new Camera(new Vector3(0, 0, 0), new Vector3(0, 0, 0), Vector3.UnitY, new Vector2i(Width, Height));

        CheckErrors();
        base.OnLoad();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        var resize = new Vector2i(e.Width, e.Width);

        Camera.Viewport = resize;
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

        SwapBuffers();
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        var state = KeyboardState.IsKeyPressed(Keys.R);

        if (!state) return;
        
        Console.WriteLine("Updating shaders.");
        foreach (var renderable in render_objects)
        {
            renderable.UpdateShader(new Shader("./Assets/Shaders/colored.vert", "./Assets/Shaders/colored.frag"));
        }
    }
}