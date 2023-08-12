using System.Diagnostics;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace ThirtyDollarVisualizer;

public class Manager
{
    private readonly Stopwatch _stopwatch = new();
    private Shader _shader = null!;

    private IndexBuffer _ibo = null!;
    private VertexArray _vao = null!;
    private VertexBuffer _vbo = null!;
    
    private GL Gl = null!;
    private readonly IWindow Window;
    private Renderer Renderer = null!;
    
    public Manager(IWindow window)
    {
        Window = window;
        Window.Load += OnLoad;
        Window.Render += OnRenderFrame;
        Window.Update += OnUpdateFrame;
        Window.Closing += OnUnload;
        Window.Resize += OnResize;
    }

    ~Manager()
    {
        Window.Load -= OnLoad;
        Window.Render -= OnRenderFrame;
        Window.Update -= OnUpdateFrame;
        Window.Closing -= OnUnload;
    }
    
    private void ClearAllErrors()
    {
        while (Gl.GetError() != (GLEnum) ErrorCode.NoError)
        {
            // Ignored
        }
    }

    private void CheckErrors()
    {
        GLEnum errorCode;
        while ((errorCode = Gl.GetError()) != (GLEnum) ErrorCode.NoError)
            Console.WriteLine($"[OpenGL Error]: (0x{(int) errorCode:x8}) \'{errorCode}\'");
    }

    protected void OnLoad()
    {
        Gl = GL.GetApi(Window);
        Renderer = new Renderer(Gl);
        
        Gl.ClearColor(.0f, .0f, .0f, 1.0f);
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

        _vao = new VertexArray(Gl);
        _vbo = new VertexBuffer(Gl, positions);
        _ibo = new IndexBuffer(Gl, indices);

        var layout = new VertexBufferLayout();
        layout.PushFloat(2);
        layout.PushFloat(2);
        _vao.AddBuffer(_vbo, layout);

        _shader = Shader.FromFiles(Gl,"./Assets/Shaders/shader.vert", "./Assets/Shaders/shader.frag");
        _stopwatch.Start();

        var texture = new Texture(Gl, "Assets/Textures/moai.png");
        texture.Bind();
        _shader.SetUniform1("u_Texture", 0);
        //_shader.SetUniformMatrix4("u_MVP", trans);

        _shader.Unbind();
        _vao.Unbind();
        _vbo.Unbind();
        _ibo.Unbind();

        CheckErrors();
    }

    protected void OnRenderFrame(double obj)
    {
        Gl.Clear(ClearBufferMask.ColorBufferBit);
        ClearAllErrors();
        _shader.Bind();

        Renderer.Draw(_vao, _ibo, _shader);
        CheckErrors();
    }

    protected void OnUpdateFrame(double obj)
    {
        
    }

    protected void OnResize(Vector2D<int> dimensions)
    {
        Gl.Viewport(0, 0, (uint) dimensions.X, (uint) dimensions.Y);
    }

    protected void OnUnload()
    {
        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        Gl.BindVertexArray(0);
        Gl.UseProgram(0);
    }
}