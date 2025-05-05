using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Objects.Planes.Uniforms;
using ThirtyDollarVisualizer.Renderer;
using ThirtyDollarVisualizer.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Objects.Planes;

public class ColoredPlane : Renderable
{
    private static bool AreVerticesGenerated;
    private static VertexArrayObject<float> Static_Vao = null!;
    private static BufferObject<float> Static_Vbo = null!;
    private static BufferObject<uint> Static_Ebo = null!;
    private static BufferObject<ColoredUniform>? UniformBuffer;

    public override Shader? Shader { get; set; } = ShaderPool.GetOrLoad(
        "colored_plane", () => new Shader("ThirtyDollarVisualizer.Assets.Shaders.colored.vert",
            "ThirtyDollarVisualizer.Assets.Shaders.colored.frag")
    );

    public float BorderRadius;
    private ColoredUniform Uniform;

    public ColoredPlane(Vector4 color) : this(color, (0, 0, 0), (0, 0, 0))
    {
    }

    public ColoredPlane(Vector4 color, Vector3 position, Vector3 scale, float border_radius = 0f)
    {
        _position = position;
        _scale = scale;

        if (!AreVerticesGenerated) SetVertices();

        Vao = Static_Vao;
        Vbo = Static_Vbo;
        Ebo = Static_Ebo;
        Color = color;

        Uniform = new ColoredUniform();
        BorderRadius = border_radius;
    }

    private void SetVertices()
    {
        lock (LockObject)
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

            Static_Vao = new VertexArrayObject<float>();
            Static_Vbo = new BufferObject<float>(vertices, BufferTarget.ArrayBuffer);

            var layout = new VertexBufferLayout();
            layout.PushFloat(3); // xyz vertex coords
            Static_Vao.AddBuffer(Static_Vbo, layout);

            Static_Ebo = new BufferObject<uint>(indices, BufferTarget.ElementArrayBuffer);
            AreVerticesGenerated = true;
        }
    }

    public override void Render(Camera camera)
    {
        lock (LockObject)
        {
            if (Ebo == null || Vao == null || Shader == null) return;

            Vao.Bind();
            Ebo.Bind();
            Shader.Use();
            SetShaderUniforms(camera);

            GL.DrawElements(PrimitiveType.Triangles, Ebo.GetCount(), DrawElementsType.UnsignedInt, 0);
        }

        base.Render(camera);
    }

    public override void SetShaderUniforms(Camera camera)
    {
        Uniform.Color = Color;
        Uniform.BorderRadiusPx = BorderRadius;

        Uniform.ScalePx = _scale.X;
        Uniform.AspectRatio = _scale.X / _scale.Y;
        Uniform.Model = Model;
        Uniform.Projection = camera.GetProjectionMatrix();

        Span<ColoredUniform> span = [Uniform];

        if (UniformBuffer is null)
        {
            UniformBuffer =
                new BufferObject<ColoredUniform>(span, BufferTarget.UniformBuffer);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, UniformBuffer.Handle);
        }
        else
        {
            UniformBuffer.SetBufferData(span, BufferTarget.UniformBuffer);
        }

        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, UniformBuffer.Handle);
    }

    public override void Dispose()
    {
    }
}