using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects.Planes;

public class TexturedPlane : Renderable
{
    private readonly BufferObject<uint> _ebo;
    private readonly BufferObject<float> _vbo;
    private readonly VertexArrayObject<float> _vao;
    private readonly Texture _texture;

    public TexturedPlane(Texture texture, Vector3 position, Vector2 width_height)
    {
        var (x, y,z ) = position;
        var (w, h) = width_height;
        Position = new Vector3(x, y, 0);
        Scale = new Vector3(w, h, 0);
        Offset = Vector3.Zero;

        var vertices = new[] {
            // Position         // Texture Coordinates
            x, y + h, z,           0.0f, 1.0f,  // Bottom-left
            x + w, y + h, z,       1.0f, 1.0f,  // Bottom-right
            x + w, y, z,           1.0f, 0.0f,  // Top-right
            x, y, z,               0.0f, 0.0f   // Top-left
        };

        var indices = new uint[] { 0, 1, 2, 0, 2, 3 };

        _vao = new VertexArrayObject<float>();
        _vbo = new BufferObject<float>(vertices, BufferTarget.ArrayBuffer);

        var layout = new VertexBufferLayout();
        layout.PushFloat(3); // xyz vertex coords
        layout.PushFloat(2); // xy texture coords
        
        _vao.AddBuffer(_vbo, layout);

        _ebo = new BufferObject<uint>(indices, BufferTarget.ElementArrayBuffer);
        
        Shader = new Shader("./Assets/Shaders/textured.vert", "./Assets/Shaders/textured.frag");
        Color = new Vector4(0, 0, 0, 0);
        
        _texture = texture;
    }

    public TexturedPlane(string texture_location, Vector3 position, Vector2 width_height): this(new Texture(texture_location), position, width_height)
    {
    }

    public override Vector3 GetScale()
    {
        return Scale;
    }

    public override void Render(Camera camera)
    {
        _vao.Bind();
        _ebo.Bind();
        _texture.Bind();
        Shader.Use();
        
        Shader.SetUniform("u_CameraPosition", camera.Position);
        Shader.SetUniform("u_ViewportSize", camera.Viewport);
        Shader.SetUniform("u_OffsetRelative", Offset);
        Shader.SetUniform("u_OverlayColor", Color);
        
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