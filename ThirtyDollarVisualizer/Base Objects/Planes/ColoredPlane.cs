using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Base_Objects.Planes.Uniforms;
using ThirtyDollarVisualizer.Engine.Asset_Management;
using ThirtyDollarVisualizer.Engine.Asset_Management.Extensions;
using ThirtyDollarVisualizer.Engine.Asset_Management.Types.Shader;
using ThirtyDollarVisualizer.Engine.Renderer;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Attributes;
using ThirtyDollarVisualizer.Engine.Renderer.Buffers;
using ThirtyDollarVisualizer.Engine.Renderer.Cameras;
using ThirtyDollarVisualizer.Engine.Renderer.Queues;
using ThirtyDollarVisualizer.Engine.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Base_Objects.Planes;

[PreloadGraphicsContext]
public class ColoredPlane : Renderable, IGamePreloadable
{
    private static DeleteQueue _deleteQueue = null!;
    private static Shader _shader = null!;

    private static bool _areVerticesGenerated;
    private static VertexArrayObject _staticVAO = null!;
    private static GLBuffer<ColoredUniform>? _uniformBuffer;

    private ColoredUniform _uniform;
    public float BorderRadius;

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

    [UsedImplicitly]
    public static void Preload(AssetProvider assetProvider)
    {
        _deleteQueue = assetProvider.DeleteQueue;
        _shader = assetProvider.ShaderPool.GetOrLoad("Assets/Shaders/ColoredPlane", provider =>
            new Shader(provider, provider.LoadShaders(
                ShaderInfo.CreateFromUnknownStorage(ShaderType.VertexShader, "Assets/Shaders/colored.vert"),
                ShaderInfo.CreateFromUnknownStorage(ShaderType.FragmentShader, "Assets/Shaders/colored.frag")))
        );
    }

    private static void SetVertices()
    {
        _staticVAO = new VertexArrayObject();
        var layout = new VertexBufferLayout();
        layout.PushFloat(3); // xyz vertex coords

        _staticVAO.AddBuffer(GLQuad.VBOWithoutUV, layout);
        _areVerticesGenerated = true;
    }

    public override void Render(Camera camera)
    {
        if (Shader == null) return;

        _staticVAO.Bind();
        _staticVAO.Update();

        GLQuad.EBO.Bind();
        GLQuad.EBO.Update();

        Shader.Use();
        SetShaderUniforms(camera);

        GL.DrawElements(PrimitiveType.Triangles, GLQuad.EBO.Capacity, DrawElementsType.UnsignedInt, 0);
        base.Render(camera);
    }

    public override void SetShaderUniforms(Camera camera)
    {
        _uniform.Color = Color;
        _uniform.BorderRadiusPx = BorderRadius;

        _uniform.ScalePx = Scale.X;
        _uniform.AspectRatio = Scale.X / Scale.Y;
        _uniform.Model = Model;
        _uniform.Projection = camera.GetVPMatrix();

        Span<ColoredUniform> span = [_uniform];

        _uniformBuffer ??= new GLBuffer<ColoredUniform>(_deleteQueue, BufferTarget.UniformBuffer);
        _uniformBuffer.Dangerous_SetBufferData(span);
        GL.BindBufferBase(BufferTarget.UniformBuffer, 0, _uniformBuffer.Handle);
    }
}