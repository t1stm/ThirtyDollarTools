using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects.Planes;

public class ColoredPlane : Renderable
{
    private readonly BufferObject<uint> _ebo;
    private readonly BufferObject<float> _vbo;
    private readonly VertexArrayObject<float> _vao;
    private readonly Shader _shader;
    private readonly Vector2 _scale;
    private readonly Vector4 _color;

    public ColoredPlane(Vector4 color, Vector2 width_height)
    {
        _scale = width_height;
        var vertices = new[] {
            -0.5f, -0.5f,
            0.5f, -0.5f,
            0.5f, 0.5f,
            -0.5f, 0.5f
        };
        var indices = new uint[] { 0,1,2, 2,3,0 };

        _vao = new VertexArrayObject<float>();
        _vbo = new BufferObject<float>(vertices, BufferTarget.ArrayBuffer);

        var layout = new VertexBufferLayout();
        layout.PushFloat(2);
        _vao.AddBuffer(_vbo, layout);

        _ebo = new BufferObject<uint>(indices, BufferTarget.ElementArrayBuffer);
        
        _shader = new Shader("./Assets/Shaders/colored.vert", "./Assets/Shaders/colored.frag");
        _color = color;
    }

    public override void Render(Camera camera)
    {
        _vao.Bind();
        _ebo.Bind();
        _shader.Use();

        _shader.SetUniform("u_Color", _color);
        
        GL.DrawElements(PrimitiveType.Triangles, _ebo.GetCount(), DrawElementsType.UnsignedInt, 0);
    }

    public override void SetPosition(Vector3 position)
    {
        Position = position;
    }

    public override void Dispose()
    {
        _vao.Dispose();
        _ebo.Dispose();
        _shader.Dispose();
        _vbo.Dispose();
    }
}