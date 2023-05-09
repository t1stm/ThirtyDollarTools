using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace ThirtyDollarVisualizer;

public class Window : GameWindow
{
    private readonly Stopwatch _stopwatch = new();
    private IndexBuffer _ibo = null!;
    private Shader _shader = null!;
    private VertexArray<float> _vao = null!;

    private VertexBuffer<float> _vbo = null!;

    public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(
        gameWindowSettings, nativeWindowSettings)
    {
    }

    private static void ClearAllErrors()
    {
        while (GL.GetError() != ErrorCode.NoError)
        {
            // Ignored
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
        GL.ClearColor(.0f, .0f, .0f, 1.0f);
        float[] positions =
        {
            -0.5f, -0.5f, 0f, 0f,
            0.5f, -0.5f, 1f, 0f,
            0.5f, 0.5f, 1f, 1f,
            -0.5f, 0.5f, 0f, 1f
        };

        uint[] indices =
        {
            0, 1, 2,
            2, 3, 0
        };

        _vao = new VertexArray<float>();
        _vbo = new VertexBuffer<float>(positions);
        _ibo = new IndexBuffer(indices);

        var layout = new VertexBufferLayout();
        layout.PushFloat(2);
        layout.PushFloat(2);
        _vao.AddBuffer(_vbo, layout);

        _shader = Shader.FromFiles("./Assets/Shaders/shader.vert", "./Assets/Shaders/shader.frag");
        _stopwatch.Start();

        var texture = new Texture("Assets/Textures/moai.png");
        texture.Bind();
        _shader.SetUniform1("u_Texture", 0);
        //_shader.SetUniformMatrix4("u_MVP", trans);

        _shader.Unbind();
        _vao.Unbind();
        _vbo.Unbind();
        _ibo.Unbind();

        CheckErrors();

        base.OnLoad();
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        GL.Clear(ClearBufferMask.ColorBufferBit);
        ClearAllErrors();
        _shader.Bind();

        Renderer.Draw(_vao, _ibo, _shader);
        CheckErrors();

        SwapBuffers();
        //base.OnRenderFrame(args);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        var mouse = MouseState;
        
        
        base.OnUpdateFrame(args);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        GL.Viewport(0, 0, e.Width, e.Height);
        base.OnResize(e);
    }

    protected override void OnUnload()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);
        GL.UseProgram(0);

        base.OnUnload();
    }
}