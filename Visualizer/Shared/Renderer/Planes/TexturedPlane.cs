using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Shared.Renderer.Planes.Uniforms;
using Sundex.Core;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Extensions;
using Sundex.Engine.Asset_Management.Types.Shader;
using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Renderer.Data_Buffers;
using Sundex.Engine.Renderer.Queues;
using Sundex.Engine.Renderer.Shaders;
using Sundex.Engine.Renderer.Textures;

namespace Shared.Renderer.Planes;

[PreloadGraphicsContext]
public class TexturedPlane : Renderable, IGamePreloadable, IBorderRadius
{
    private static DeleteQueue _deleteQueue = null!;
    private static Shader _shader = null!;

    private static bool _areVerticesGenerated;
    private static VertexArrayObject _staticVAO = null!;
    private static GLBuffer<TexturedUniform>? _uniformBuffer;

    private TexturedUniform _uniform;

    public TexturedPlane()
    {
        if (!_areVerticesGenerated) SetVertices();
        _uniform = new TexturedUniform();

        // The shader multiplies the sampled texel by this, so the inherited default of
        // zero would draw nothing. White is "the texture as it is"; a caller lowers the
        // alpha to fade it (ElementAlpha does exactly that) or the RGB to tint it.
        Color = Vector4.One;
    }

    public GPUTexture? Texture { get; set; }

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

    public override Shader Shader
    {
        get => _shader;
        set => _shader = value ?? throw new ArgumentNullException(nameof(value));
    }

    public float BorderRadius { get; set; }

    [UsedImplicitly]
    public static void Preload(AssetProvider assetProvider)
    {
        _deleteQueue = assetProvider.DeleteQueue;
        _shader = assetProvider.ShaderPool.GetOrLoad("Assets/Shaders/TexturedPlane", provider =>
            new Shader(provider, provider.LoadShaders(
                ShaderInfo.CreateFromUnknownStorage(ShaderType.VertexShader,
                    "Assets/Shaders/Planes/Textured/textured.vert"),
                ShaderInfo.CreateFromUnknownStorage(ShaderType.FragmentShader,
                    "Assets/Shaders/Planes/Textured/textured.frag")))
        );
    }

    private static void SetVertices()
    {
        _staticVAO = new VertexArrayObject();
        var layout = new VertexBufferLayout();
        layout.PushFloat(3); // xyz vertex coords
        layout.PushFloat(2); // uv texture coords

        _staticVAO.AddBuffer(GLQuad.VBOWithUV, layout);
        _staticVAO.SetIndexBuffer(GLQuad.EBO);
        _areVerticesGenerated = true;
    }

    public override void Render(Camera camera)
    {
        _staticVAO.Bind();
        _staticVAO.Update();

        Texture?.Bind();
        Shader.Use();
        SetShaderUniforms(camera);

        GL.DrawElements(PrimitiveType.Triangles, GLQuad.EBO.Capacity, DrawElementsType.UnsignedInt, 0);
        base.Render(camera);
    }

    public override void SetShaderUniforms(Camera camera)
    {
        _uniform.ScaleAndBorderPx = new Vector4(Scale.X, Scale.Y, BorderRadius, 0);
        _uniform.Color = Color;

        _uniform.Model = Model;
        _uniform.Projection = camera.GetVPMatrix();

        Span<TexturedUniform> span = [_uniform];

        _uniformBuffer ??= new GLBuffer<TexturedUniform>(_deleteQueue, BufferTarget.UniformBuffer);
        _uniformBuffer.Dangerous_SetBufferData(span);
        GL.BindBufferBase(BufferTarget.UniformBuffer, 0, _uniformBuffer.Handle);
    }
}