using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects.Planes;

public sealed class ColoredPlane : Renderable
{
    public ColoredPlane(Vector4 color, Vector3 position, Vector2 width_height)
    {
        var (w, h) = width_height;
        
        Position = new Vector3(position);
        Scale = new Vector3(w, h, 0);
        Offset = new Vector3(Vector3.Zero);
        
        UpdateVertices();
        
        Shader = new Shader("ThirtyDollarVisualizer.Assets.Shaders.colored.vert", "ThirtyDollarVisualizer.Assets.Shaders.colored.frag");
        Color = color;
    }

    public ColoredPlane(Vector4 color, Vector3 position, Vector2 width_height, Shader? shader) : this(color, position, width_height)
    {
        Shader = shader ?? Shader;
    }

    public override void UpdateVertices()
    {
        lock (LockObject)
        {
            var (x, y, z) = Position;
            var (w, h, _) = Scale;
        
            var vertices = new[] {
                x, y + h, z,
                x + w, y + h, z,
                x + w, y, z,
                x, y, z
            };
        
            var indices = new uint[] { 0,1,2, 0,2,3 };

            Vao = new VertexArrayObject<float>();
            Vbo = new BufferObject<float>(vertices, BufferTarget.ArrayBuffer);

            var layout = new VertexBufferLayout();
            layout.PushFloat(3); // xyz vertex coords
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
            Shader.Use();

            Shader.SetUniform("u_Color", Color);
            Shader.SetUniform("u_ViewportSize", camera.Viewport);
            Shader.SetUniform("u_OffsetRelative", Offset);
        
            GL.DrawElements(PrimitiveType.Triangles, Ebo.GetCount(), DrawElementsType.UnsignedInt, 0);
        }
        
        base.Render(camera);
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