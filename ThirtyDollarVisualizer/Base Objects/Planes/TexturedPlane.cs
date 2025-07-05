using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Assets;
using ThirtyDollarVisualizer.Base_Objects.Planes.Uniforms;
using ThirtyDollarVisualizer.Base_Objects.Textures;
using ThirtyDollarVisualizer.Base_Objects.Textures.Static;
using ThirtyDollarVisualizer.Renderer;
using ThirtyDollarVisualizer.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Base_Objects.Planes;

public class TexturedPlane : Renderable
{
    private static bool _areVerticesGenerated;
    private static VertexArrayObject _staticVao = null!;
    private static BufferObject<float> _staticVbo = null!;
    private static BufferObject<uint> _staticEbo = null!;

    private static BufferObject<TexturedUniform>? _uniformBuffer;
    private SingleTexture? _texture;
    private TexturedUniform _uniform;

    public TexturedPlane(SingleTexture texture) : this()
    {
        _texture = texture;
    }

    public TexturedPlane()
    {
        if (!_areVerticesGenerated) SetVertices();
    }

    public override Vector3 Position
    {
        get => base.Position;
        set
        {
            base.Position = value;
            UpdateModel(IsChild);
        }
    }

    public override Vector3 Scale
    {
        get => base.Scale;
        set
        {
            base.Scale = value;
            UpdateModel(IsChild);
        }
    }

    private Lazy<Shader> _shader = new(() => ShaderPool.GetOrLoad("textured_plane", () =>
        Shader.NewVertexFragment(
            Asset.Embedded("Shaders/textured.vert"),
            Asset.Embedded("Shaders/textured.frag"))
    ));

    public override Shader? Shader
    {
        get => _shader.Value;
        set => _shader = new Lazy<Shader>(value ?? throw new ArgumentNullException(nameof(value)));
    }

    private static void SetVertices()
    {
        var (x, y, z) = (0f, 0f, 0);
        var (w, h) = (1f, 1f);

        var vertices = new[]
        {
            // Position // Texture Coordinates
            x, y + h, z, 0.0f, 1.0f, // Bottom-left
            x + w, y + h, z, 1.0f, 1.0f, // Bottom-right
            x + w, y, z, 1.0f, 0.0f, // Top-right
            x, y, z, 0.0f, 0.0f // Top-left
        };

        var indices = new uint[] { 0, 1, 3, 1, 2, 3 };

        _staticVao = new VertexArrayObject();
        _staticVbo = new BufferObject<float>(vertices, BufferTarget.ArrayBuffer);

        var layout = new VertexBufferLayout();
        layout.PushFloat(3); // xyz vertex coords
        layout.PushFloat(2); // wh frag coords
        _staticVao.AddBuffer(_staticVbo, layout);

        _staticEbo = new BufferObject<uint>(indices, BufferTarget.ElementArrayBuffer);
        _areVerticesGenerated = true;
    }

    public override void Render(Camera camera)
    {
        if (!IsVisible) return;
        if (Shader == null) return;

        var texture = _texture ?? StaticTexture.TransparentPixel;
        if (texture.NeedsUploading())
            texture.UploadToGPU();

        texture.Bind();
        _staticVao.Bind();
        _staticEbo.Bind();
        Shader.Use();
        SetShaderUniforms(camera);

        GL.DrawElements(PrimitiveType.Triangles, _staticEbo.GetCount(), DrawElementsType.UnsignedInt, 0);

        base.Render(camera);
    }

    public override void SetShaderUniforms(Camera camera)
    {
        _uniform.Model = Model;
        _uniform.Projection = camera.GetVPMatrix();
        _uniform.DeltaAlpha = InverseAlpha;

        Span<TexturedUniform> span = [_uniform];
        if (_uniformBuffer is null)
            _uniformBuffer =
                new BufferObject<TexturedUniform>(span, BufferTarget.UniformBuffer);
        else _uniformBuffer.SetBufferData(span, BufferTarget.UniformBuffer);
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _uniformBuffer.Handle);
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