using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects.Planes;

public sealed class TexturedPlane : Renderable
{
    private readonly Texture _texture;
    private static bool AreVerticesGenerated;
    private static VertexArrayObject<float> Static_Vao = null!;
    private static BufferObject<float> Static_Vbo = null!;
    private static BufferObject<uint> Static_Ebo = null!;

    public TexturedPlane(Texture texture, Vector3 position, Vector2 width_height)
    {
        _position = new Vector3(position);
        _scale = new Vector3(width_height.X, width_height.Y, 0);
        
        if (!AreVerticesGenerated) SetVertices();
        UpdateModel();
        
        Vao = Static_Vao;
        Vbo = Static_Vbo;
        Ebo = Static_Ebo;
        
        Shader = new Shader("ThirtyDollarVisualizer.Assets.Shaders.textured.vert", "ThirtyDollarVisualizer.Assets.Shaders.textured.frag");
        Color = new Vector4(0, 0, 0, 0);
        
        _texture = texture;
    }

    public override void SetVertices()
    {
        lock (LockObject)
        {
            var (x, y, z) = (0f, 0f, 0);
            var (w, h) = (1f, 1f);
            
            var vertices = new[] {
                // Position         // Texture Coordinates
                x, y + h, z,           0.0f, 1.0f,  // Bottom-left
                x + w, y + h, z,       1.0f, 1.0f,  // Bottom-right
                x + w, y, z,           1.0f, 0.0f,  // Top-right
                x, y, z,               0.0f, 0.0f   // Top-left
            };

            var indices = new uint[] { 0, 1, 3, 1, 2, 3 };

            Static_Vao = new VertexArrayObject<float>();
            Static_Vbo = new BufferObject<float>(vertices, BufferTarget.ArrayBuffer);

            var layout = new VertexBufferLayout();
            layout.PushFloat(3); // xyz vertex coords
            layout.PushFloat(2); // wh vertex coords
            Static_Vao.AddBuffer(Static_Vbo, layout);

            Static_Ebo = new BufferObject<uint>(indices, BufferTarget.ElementArrayBuffer);
            AreVerticesGenerated = true;
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

            Shader.SetUniform("u_Model", Model);
            Shader.SetUniform("u_Projection", camera.GetProjectionMatrix());
            
            Shader.SetUniform("u_OverlayColor", Color);

            GL.DrawElements(PrimitiveType.Triangles, Ebo.GetCount(), DrawElementsType.UnsignedInt, 0);
        }
        
        base.Render(camera);
    }

    public override void Dispose()
    {
        lock (LockObject)
        {
            Shader.Dispose();
        }
    }
}