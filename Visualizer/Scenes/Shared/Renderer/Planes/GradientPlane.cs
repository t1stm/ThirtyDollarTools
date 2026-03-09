using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Shared.Renderer.Planes.Uniforms;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Extensions;
using Sundex.Engine.Asset_Management.Types.Shader;
using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Renderer.Buffers;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Renderer.Queues;
using Sundex.Engine.Renderer.Shaders;

namespace Shared.Renderer.Planes;

[PreloadGraphicsContext]
public class GradientPlane : Renderable, IGamePreloadable, IBorderRadius
{
    private static DeleteQueue _deleteQueue = null!;
    private static Shader _shader = null!;

    private static bool _areVerticesGenerated;
    private static VertexArrayObject _staticVAO = null!;
    private static GLBuffer<GradientUniform>? _uniformBuffer;

    private GradientUniform _uniform;

    public GradientPlane()
    {
        if (!_areVerticesGenerated) SetVertices();
        _uniform = new GradientUniform();
    }

    public float BorderRadius { get; set; }
    public GradientType GradientType { get; set; } = GradientType.Linear;
    public List<Vector4> GradientColors { get; set; } = [];
    public List<float> GradientStops { get; set; } = [];

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

    [UsedImplicitly]
    public static void Preload(AssetProvider assetProvider)
    {
        _deleteQueue = assetProvider.DeleteQueue;
        _shader = assetProvider.ShaderPool.GetOrLoad("Assets/Shaders/GradientPlane", provider =>
            new Shader(provider, provider.LoadShaders(
                ShaderInfo.CreateFromUnknownStorage(ShaderType.VertexShader,
                    "Assets/Shaders/Planes/Gradient/gradient.vert"),
                ShaderInfo.CreateFromUnknownStorage(ShaderType.FragmentShader,
                    "Assets/Shaders/Planes/Gradient/gradient.frag")))
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
        _uniform.Model = Model;
        _uniform.Projection = camera.GetVPMatrix();
        _uniform.ScaleAndBorderPx = new Vector4(Scale.X, Scale.Y, BorderRadius, 0);
        _uniform.GradientType = GradientType;
        _uniform.GradientStopCount = Math.Min(GradientColors.Count, 8);

        for (var i = 0; i < _uniform.GradientStopCount; i++)
        {
            var color = GradientColors[i];
            var stop = i < GradientStops.Count
                ? GradientStops[i]
                : (float)i / Math.Max(1, _uniform.GradientStopCount - 1);

            switch (i)
            {
                case 0:
                    _uniform.Color0 = color;
                    _uniform.Stop0 = stop;
                    break;
                case 1:
                    _uniform.Color1 = color;
                    _uniform.Stop1 = stop;
                    break;
                case 2:
                    _uniform.Color2 = color;
                    _uniform.Stop2 = stop;
                    break;
                case 3:
                    _uniform.Color3 = color;
                    _uniform.Stop3 = stop;
                    break;
                case 4:
                    _uniform.Color4 = color;
                    _uniform.Stop4 = stop;
                    break;
                case 5:
                    _uniform.Color5 = color;
                    _uniform.Stop5 = stop;
                    break;
                case 6:
                    _uniform.Color6 = color;
                    _uniform.Stop6 = stop;
                    break;
                case 7:
                    _uniform.Color7 = color;
                    _uniform.Stop7 = stop;
                    break;
            }
        }

        Span<GradientUniform> span = [_uniform];

        _uniformBuffer ??= new GLBuffer<GradientUniform>(_deleteQueue, BufferTarget.UniformBuffer);
        _uniformBuffer.Dangerous_SetBufferData(span);
        GL.BindBufferBase(BufferTarget.UniformBuffer, 0, _uniformBuffer.Handle);
    }
}