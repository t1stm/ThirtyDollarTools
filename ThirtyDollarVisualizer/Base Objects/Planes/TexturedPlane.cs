using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Objects.Planes.Uniforms;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects.Planes;

public class TexturedPlane : Renderable
{
    private static bool AreVerticesGenerated;
    private static VertexArrayObject<float> Static_Vao = null!;
    private static BufferObject<float> Static_Vbo = null!;
    private static BufferObject<uint> Static_Ebo = null!;
    
    private BufferObject<TexturedUniform>? UniformBuffer;
    private TexturedUniform Uniform;
    protected Texture? _texture;

    public TexturedPlane(Texture texture, Vector3 position, Vector3 scale)
    {
        _position = new Vector3(position);
        _scale = scale;

        if (!AreVerticesGenerated) SetVertices();

        Vao = Static_Vao;
        Vbo = Static_Vbo;
        Ebo = Static_Ebo;
        
        Uniform = new TexturedUniform();

        Shader = new Shader("ThirtyDollarVisualizer.Assets.Shaders.textured.vert",
            "ThirtyDollarVisualizer.Assets.Shaders.textured.frag");
        Color = new Vector4(0, 0, 0, 0);

        _texture = texture;
    }

    public TexturedPlane() : this(Texture.Transparent1x1, Vector3.Zero, Vector2.One)
    {
    }

    public TexturedPlane(Texture texture) : this(texture, Vector3.Zero, (texture.Width, texture.Height))
    {
    }

    public TexturedPlane(Texture texture, Vector3 position, Vector2 scale) :
        this(texture, position, new Vector3(scale))
    {
    }

    private void SetVertices()
    {
        lock (LockObject)
        {
            var (x, y, z) = (0f, 0f, 0);
            var (w, h) = (1f, 1f);

            var vertices = new[]
            {
                // Position         // Texture Coordinates
                x, y + h, z, 0.0f, 1.0f, // Bottom-left
                x + w, y + h, z, 1.0f, 1.0f, // Bottom-right
                x + w, y, z, 1.0f, 0.0f, // Top-right
                x, y, z, 0.0f, 0.0f // Top-left
            };

            var indices = new uint[] { 0, 1, 3, 1, 2, 3 };

            Static_Vao = new VertexArrayObject<float>();
            Static_Vbo = new BufferObject<float>(vertices, BufferTarget.ArrayBuffer);

            var layout = new VertexBufferLayout();
            layout.PushFloat(3); // xyz vertex coords
            layout.PushFloat(2); // wh frag coords
            Static_Vao.AddBuffer(Static_Vbo, layout);

            Static_Ebo = new BufferObject<uint>(indices, BufferTarget.ElementArrayBuffer);
            AreVerticesGenerated = true;
        }
    }

    public override void Render(Camera camera)
    {
        if (!IsVisible) return;

        lock (LockObject)
        {
            if (Ebo == null || Vao == null) return;
            Vao.Bind();
            Ebo.Bind();

            var texture = _texture ?? Texture.Transparent1x1;

            if (texture.NeedsLoading()) texture.LoadOpenGLTexture();

            texture.Bind();
            Shader.Use();
            SetShaderUniforms(camera);

            GL.DrawElements(PrimitiveType.Triangles, Ebo.GetCount(), DrawElementsType.UnsignedInt, 0);
        }

        base.Render(camera);
    }

    public override void SetShaderUniforms(Camera camera)
    {
        Uniform.Model = Model;
        Uniform.Projection = camera.GetProjectionMatrix();
        Uniform.DeltaAlpha = DeltaAlpha;
        
        Span<TexturedUniform> span = stackalloc TexturedUniform[] { Uniform };
        if (UniformBuffer is null)
        {
            UniformBuffer = new BufferObject<TexturedUniform>(span, BufferTarget.UniformBuffer, BufferUsageHint.StreamDraw);
        }
        else UniformBuffer.SetBufferData(span, BufferTarget.UniformBuffer, BufferUsageHint.StreamDraw);
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, UniformBuffer.Handle);
    }

    public override void Dispose()
    {
    }

    public void SetTexture(Texture? texture)
    {
        lock (LockObject)
        {
            _texture = texture;
        }
    }

    public Texture? GetTexture()
    {
        return _texture;
    }
}