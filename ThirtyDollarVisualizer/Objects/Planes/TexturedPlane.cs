using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects.Planes;

public sealed class TexturedPlane : Renderable
{
    private readonly Texture _texture;

    public TexturedPlane(Texture texture, Vector3 position, Vector2 width_height)
    {
        var (w, h) = width_height;

        Position = new Vector3(position);
        Scale = new Vector3(w, h, 0);
        Offset = new Vector3(Vector3.Zero);
        
        UpdateVertices();
        
        Shader = new Shader("./Assets/Shaders/textured.vert", "./Assets/Shaders/textured.frag");
        Color = new Vector4(0, 0, 0, 0);
        
        _texture = texture;
    }

    public override void UpdateVertices()
    {
        lock (LockObject)
        {
            var (x, y, z) = Position;
            var (w, h, _) = Scale;
        
            var vertices = new[] {
                // Position         // Texture Coordinates
                x, y + h, z,           0.0f, 1.0f,  // Bottom-left
                x + w, y + h, z,       1.0f, 1.0f,  // Bottom-right
                x + w, y, z,           1.0f, 0.0f,  // Top-right
                x, y, z,               0.0f, 0.0f   // Top-left
            };

            var indices = new uint[] { 0, 1, 2, 0, 2, 3 };

            Vao = new VertexArrayObject<float>();
            Vbo = new BufferObject<float>(vertices, BufferTarget.ArrayBuffer);

            var layout = new VertexBufferLayout();
            layout.PushFloat(3); // xyz vertex coords
            layout.PushFloat(2); // xy texture coords
        
            Vao.AddBuffer(Vbo, layout);
            Ebo = new BufferObject<uint>(indices, BufferTarget.ElementArrayBuffer);
        }
    }

    public override void Render(Camera camera)
    {
        lock (LockObject)
        {
            if (Ebo == null || Vao == null) return;
            Vao.Bind();
            Ebo.Bind();
        
            _texture.Bind();
            Shader.Use();

            Shader.SetUniform("u_CameraPosition", camera.Position);
            Shader.SetUniform("u_ViewportSize", camera.Viewport);
            Shader.SetUniform("u_OffsetRelative", Offset);
            Shader.SetUniform("u_OverlayColor", Color);
        
            GL.DrawElements(PrimitiveType.Triangles, Ebo.GetCount(), DrawElementsType.UnsignedInt, 0);
        }
    }

    public override void Dispose()
    {
        lock (LockObject)
        {
            Vao?.Dispose();
            Ebo?.Dispose();
            Vbo?.Dispose();
            Shader.Dispose();
        }
    }
}