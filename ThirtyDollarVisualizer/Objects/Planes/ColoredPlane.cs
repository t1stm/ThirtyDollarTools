using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects.Planes;

public class ColoredPlane : Renderable
{
    private readonly BufferObject<uint> _ebo;
    private readonly BufferObject<float> _vbo;
    private readonly VertexArrayObject<float> _vao;

    public ColoredPlane(Vector4 color, Vector3 position, Vector2 width_height)
    {
        var (x, y,z ) = position;
        var (w, h) = width_height;
        Position = new Vector3(x, y, 0);
        Scale = new Vector3(w, h, 0);
        Offset = Vector3.Zero;

        var vertices = new[] {
             x, y + h, z,
             x + w, y + h, z,
             x + w, y, z,
             x, y, z
        };
        
        var indices = new uint[] { 0,1,2, 0,2,3 };

        _vao = new VertexArrayObject<float>();
        _vbo = new BufferObject<float>(vertices, BufferTarget.ArrayBuffer);

        var layout = new VertexBufferLayout();
        layout.PushFloat(3); // xyz vertex coords
        _vao.AddBuffer(_vbo, layout);

        _ebo = new BufferObject<uint>(indices, BufferTarget.ElementArrayBuffer);
        
        Shader = new Shader("./Assets/Shaders/colored.vert", "./Assets/Shaders/colored.frag");
        Color = color;
    }

    public override Vector3 GetScale()
    {
        return Scale;
    }

    public override void Render(Camera camera)
    {
        _vao.Bind();
        _ebo.Bind();
        Shader.Use();

        Shader.SetUniform("u_Color", Color);
        Shader.SetUniform("u_ViewportSize", camera.Viewport);
        Shader.SetUniform("u_OffsetRelative", Offset);
        
        GL.DrawElements(PrimitiveType.Triangles, _ebo.GetCount(), DrawElementsType.UnsignedInt, 0);
    }

    public override void SetPosition(Vector3 position)
    {
        Position = position;
    }
    
    public override void SetOffset(Vector3 position)
    {
        Offset = position;
    }
    
    public override void SetColor(Vector4 color)
    {
        Color = color;
    }

    public override void ChangeShader(Shader shader)
    {
        Shader = shader;
    }

    public override void Dispose()
    {
        _vao.Dispose();
        _ebo.Dispose();
        Shader.Dispose();
        _vbo.Dispose();
    }
}