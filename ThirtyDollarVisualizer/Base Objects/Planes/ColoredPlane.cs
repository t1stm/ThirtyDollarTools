using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Base_Objects.Planes.Uniforms;
using ThirtyDollarVisualizer.Renderer;
using ThirtyDollarVisualizer.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Base_Objects.Planes;

public class ColoredPlane : Renderable
{
    private static bool _areVerticesGenerated;
    private static VertexArrayObject _staticVAO = null!;
    private static BufferObject<float> _staticVBO = null!;
    private static BufferObject<uint> _staticEBO = null!;
    private static BufferObject<ColoredUniform>? _uniformBuffer;

    private ColoredUniform _uniform;
    public float BorderRadius;


    public ColoredPlane()
    {
        if (!_areVerticesGenerated) SetVertices();
        _uniform = new ColoredUniform();
    }

    public override Shader? Shader { get; set; } = ShaderPool.GetOrLoad(
        "colored_plane", () => Shader.NewVertexFragment("ThirtyDollarVisualizer.Assets.Shaders.colored.vert",
            "ThirtyDollarVisualizer.Assets.Shaders.colored.frag")
    );

    private void SetVertices()
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
        _staticVBO = new BufferObject<float>(vertices, BufferTarget.ArrayBuffer);

        var layout = new VertexBufferLayout();
        layout.PushFloat(3); // xyz vertex coords
        _staticVAO.AddBuffer(_staticVBO, layout);

        _staticEBO = new BufferObject<uint>(indices, BufferTarget.ElementArrayBuffer);
        _areVerticesGenerated = true;
    }

    public override void Render(Camera camera)
    {
        if (Shader == null) return;

        _staticVAO.Bind();
        _staticEBO.Bind();
        Shader.Use();
        SetShaderUniforms(camera);

        GL.DrawElements(PrimitiveType.Triangles, _staticEBO.GetCount(), DrawElementsType.UnsignedInt, 0);
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

        if (_uniformBuffer is null)
        {
            _uniformBuffer =
                new BufferObject<ColoredUniform>(span, BufferTarget.UniformBuffer);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _uniformBuffer.Handle);
        }
        else
        {
            _uniformBuffer.SetBufferData(span, BufferTarget.UniformBuffer);
        }

        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _uniformBuffer.Handle);
    }
}