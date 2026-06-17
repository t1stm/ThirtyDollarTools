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
using Sundex.Engine.Renderer.Data_Buffers;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Renderer.Queues;
using Sundex.Engine.Renderer.Shaders;

namespace Shared.Renderer.Planes;

[PreloadGraphicsContext]
public class ColoredPlane : Renderable, IGamePreloadable, IBorderRadius
{
    private static DeleteQueue _deleteQueue = null!;
    private static Shader _shader = null!;

    private static bool _areVerticesGenerated;
    private static VertexArrayObject _staticVAO = null!;
    private static GLBuffer<ColoredUniform>? _uniformBuffer;

    private ColoredUniform _uniform;

    public ColoredPlane()
    {
        if (!_areVerticesGenerated) SetVertices();
        _uniform = new ColoredUniform();
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
        _shader = assetProvider.ShaderPool.GetOrLoad("Assets/Shaders/ColoredPlane", provider =>
            new Shader(provider, provider.LoadShaders(
                ShaderInfo.CreateFromUnknownStorage(ShaderType.VertexShader,
                    "Assets/Shaders/Planes/Colored/colored.vert"),
                ShaderInfo.CreateFromUnknownStorage(ShaderType.FragmentShader,
                    "Assets/Shaders/Planes/Colored/colored.frag")))
        );
    }

    private static void SetVertices()
    {
        _staticVAO = new VertexArrayObject();
        var layout = new VertexBufferLayout();
        layout.PushFloat(3); // xyz vertex coords

        _staticVAO.AddBuffer(GLQuad.VBOWithoutUV, layout);
        _staticVAO.SetIndexBuffer(GLQuad.EBO);
        _areVerticesGenerated = true;
    }

    public override void Render(Camera camera)
    {
        _staticVAO.Bind();
        _staticVAO.Update();

        Shader.Use();
        SetShaderUniforms(camera);

        GL.DrawElements(PrimitiveType.Triangles, GLQuad.EBO.Capacity, DrawElementsType.UnsignedInt, 0);
        base.Render(camera);
    }

    public override void SetShaderUniforms(Camera camera)
    {
        _uniform.Color = Color;
        _uniform.ScaleAndBorderPx = new Vector4(Scale.X, Scale.Y, BorderRadius, 0);

        _uniform.Model = Model;
        _uniform.Projection = camera.GetVPMatrix();

        Span<ColoredUniform> span = [_uniform];

        _uniformBuffer ??= new GLBuffer<ColoredUniform>(_deleteQueue, BufferTarget.UniformBuffer);
        _uniformBuffer.Dangerous_SetBufferData(span);
        GL.BindBufferBase(BufferTarget.UniformBuffer, 0, _uniformBuffer.Handle);
    }
}