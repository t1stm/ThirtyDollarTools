using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Assets;
using ThirtyDollarVisualizer.Base_Objects.Planes.Uniforms;
using ThirtyDollarVisualizer.Renderer;
using ThirtyDollarVisualizer.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Base_Objects.Planes;

public class ColoredPlane : Renderable
{
    private static bool _areVerticesGenerated;
    private static VertexArrayObject _staticVAO = null!;
    private static GLBuffer<float> _staticVBO = null!;
    private static GLBuffer<uint> _staticEBO = null!;
    private static GLBuffer<ColoredUniform>? _uniformBuffer;

    private Lazy<Shader> _shader = new(() => ShaderPool.GetOrLoad(
        "colored_plane", static () => Shader.NewVertexFragment(
            Asset.Embedded("Shaders/colored.vert"),
            Asset.Embedded("Shaders/colored.frag")
        )
    ), LazyThreadSafetyMode.None);

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
            x, y + h, z,
            x + w, y + h, z,
            x + w, y, z,
            x, y, z
        };

        var indices = new uint[] { 0, 1, 3, 1, 2, 3 };

        _staticVAO = new VertexArrayObject();
        _staticVBO = new GLBuffer<float>(BufferTarget.ArrayBuffer);
        _staticVBO.SetBufferData(vertices);

        var layout = new VertexBufferLayout();
        layout.PushFloat(3); // xyz vertex coords
        _staticVAO.AddBuffer(_staticVBO, layout);

        _staticEBO = new GLBuffer<uint>(BufferTarget.ElementArrayBuffer);
        _staticEBO.SetBufferData(indices);

        _areVerticesGenerated = true;
    }

    public override void Render(Camera camera)
    {
        if (Shader == null) return;

        _staticVAO.Bind();
        _staticEBO.Bind();
        Shader.Use();
        SetShaderUniforms(camera);

        GL.DrawElements(PrimitiveType.Triangles, _staticEBO.Capacity, DrawElementsType.UnsignedInt, 0);
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

        _uniformBuffer ??= new GLBuffer<ColoredUniform>(BufferTarget.UniformBuffer);
        _uniformBuffer.SetBufferData(span);
        GL.BindBufferBase(BufferTarget.UniformBuffer, 0, _uniformBuffer.Handle);
    }
}