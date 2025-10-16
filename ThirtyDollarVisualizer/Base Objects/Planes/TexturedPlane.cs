using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Assets;
using ThirtyDollarVisualizer.Base_Objects.Planes.Uniforms;
using ThirtyDollarVisualizer.Base_Objects.Textures;
using ThirtyDollarVisualizer.Base_Objects.Textures.Static;
using ThirtyDollarVisualizer.Renderer;
using ThirtyDollarVisualizer.Renderer.Abstract;
using ThirtyDollarVisualizer.Renderer.Attributes;
using ThirtyDollarVisualizer.Renderer.Buffers;
using ThirtyDollarVisualizer.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Base_Objects.Planes;

[PreloadGL]
public class TexturedPlane : Renderable, IGLPreloadable
{
    [UsedImplicitly]
    public static void Preload()
    {
        ShaderPool.PreloadShader(
            "textured_plane", static () =>
                Shader.NewVertexFragment(
                    Asset.Embedded("Shaders/textured.vert"),
                    Asset.Embedded("Shaders/textured.frag")
                )
        );
    }

    private static readonly VertexArrayObject StaticVao = new();
    private static bool _areVerticesGenerated;

    private static GLBuffer<TexturedUniform>? _uniformBuffer;

    private Lazy<Shader> _shader = new(() => ShaderPool.GetNamedShader("textured_plane"));

    private SingleTexture? _texture;

    private TexturedUniform _uniform;

    public TexturedPlane(SingleTexture texture) : this()
    {
        Texture = texture;
    }

    public TexturedPlane()
    {
        if (!_areVerticesGenerated) SetVertices();
    }

    public SingleTexture? Texture
    {
        get => _texture;
        set
        {
            _texture = value;
            if (value is null)
                return;

            Scale = new Vector3(value.Width, value.Height, 1);
        }
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

    public override Shader? Shader
    {
        get => _shader.Value;
        set => _shader = new Lazy<Shader>(value ?? throw new ArgumentNullException(nameof(value)));
    }

    private static void SetVertices()
    {
        var layout = new VertexBufferLayout();
        layout.PushFloat(3); // xyz vertex coords
        layout.PushFloat(2); // wh frag coords
        
        StaticVao.AddBuffer(GLQuad.VBOWithUV, layout);
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
        StaticVao.Bind();
        StaticVao.Update();
        
        GLQuad.EBO.Bind();
        GLQuad.EBO.Update();
        
        Shader.Use();
        SetShaderUniforms(camera);

        GL.DrawElements(PrimitiveType.Triangles, GLQuad.EBO.Capacity, DrawElementsType.UnsignedInt, 0);

        base.Render(camera);
    }

    public override void SetShaderUniforms(Camera camera)
    {
        _uniform.Model = Model;
        _uniform.Projection = camera.GetVPMatrix();
        _uniform.DeltaAlpha = InverseAlpha;

        Span<TexturedUniform> span = [_uniform];
        _uniformBuffer ??= new GLBuffer<TexturedUniform>(BufferTarget.UniformBuffer);
        _uniformBuffer.DangerousGLThread_SetBufferData(span);

        GL.BindBufferBase(BufferTarget.UniformBuffer, 0, _uniformBuffer.Handle);
    }
}