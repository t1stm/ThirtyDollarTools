using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Objects.Planes.Uniforms;
using ThirtyDollarVisualizer.Objects.Textures;
using ThirtyDollarVisualizer.Objects.Textures.Static;
using ThirtyDollarVisualizer.Renderer;
using ThirtyDollarVisualizer.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Objects.Planes;

public class TexturedPlane : Renderable
{
    private static bool AreVerticesGenerated;
    private static VertexArrayObject Static_Vao = null!;
    private static BufferObject<float> Static_Vbo = null!;
    private static BufferObject<uint> Static_Ebo = null!;

    public override Vector3 Position { get; set; }
    public override Vector3 Scale { get; set; } = Vector3.One;

    private static BufferObject<TexturedUniform>? UniformBuffer;
    private SingleTexture? _texture;
    private TexturedUniform Uniform;

    public override Shader? Shader { get; set; } = ShaderPool.GetOrLoad("textured_plane", () =>
        new Shader("ThirtyDollarVisualizer.Assets.Shaders.textured.vert",
            "ThirtyDollarVisualizer.Assets.Shaders.textured.frag"));

    public TexturedPlane(SingleTexture texture) : this()
    {
        _texture = texture;
    }

    public TexturedPlane()
    {
        if (!AreVerticesGenerated) SetVertices();
    }

    public TexturedPlane(SingleTexture texture, Vector3 position, Vector2 scale)
    {
           
    }

    private void SetVertices()
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

        Static_Vao = new VertexArrayObject();
        Static_Vbo = new BufferObject<float>(vertices, BufferTarget.ArrayBuffer);

        var layout = new VertexBufferLayout();
        layout.PushFloat(3); // xyz vertex coords
        layout.PushFloat(2); // wh frag coords
        Static_Vao.AddBuffer(Static_Vbo, layout);

        Static_Ebo = new BufferObject<uint>(indices, BufferTarget.ElementArrayBuffer);
        AreVerticesGenerated = true;
    }

    public override void Render(Camera camera)
    {
        if (!IsVisible) return;
        if (Shader == null) return;

        var texture = _texture ?? StaticTexture.Transparent1x1;
        if (texture.NeedsUploading()) 
            texture.UploadToGPU();
            
        texture.Bind();
        Static_Vao.Bind();
        Static_Ebo.Bind();
        Shader.Use();
        SetShaderUniforms(camera);

        GL.DrawElements(PrimitiveType.Triangles, Static_Ebo.GetCount(), DrawElementsType.UnsignedInt, 0);
        
        base.Render(camera);
    }

    public override void SetShaderUniforms(Camera camera)
    {
        Uniform.Model = Model;
        Uniform.Projection = camera.GetVPMatrix();
        Uniform.DeltaAlpha = DeltaAlpha;

        Span<TexturedUniform> span = [Uniform];
        if (UniformBuffer is null)
            UniformBuffer =
                new BufferObject<TexturedUniform>(span, BufferTarget.UniformBuffer);
        else UniformBuffer.SetBufferData(span, BufferTarget.UniformBuffer);
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, UniformBuffer.Handle);
    }

    public void SetTexture(SingleTexture? texture)
    {
        _texture = texture;
    }

    public SingleTexture? GetTexture()
    {
        return _texture;
    }
}