using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Objects.Planes.Uniforms;
using ThirtyDollarVisualizer.Renderer;
using ThirtyDollarVisualizer.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Objects.Planes;

public class ColoredPlane : Renderable
{
    private static bool AreVerticesGenerated;
    private static VertexArrayObject Static_Vao = null!;
    private static BufferObject<float> Static_Vbo = null!;
    private static BufferObject<uint> Static_Ebo = null!;
    private static BufferObject<ColoredUniform>? UniformBuffer;

    public override Shader? Shader { get; set; } = ShaderPool.GetOrLoad(
        "colored_plane", () => new Shader("ThirtyDollarVisualizer.Assets.Shaders.colored.vert",
            "ThirtyDollarVisualizer.Assets.Shaders.colored.frag")
    );

    public float BorderRadius;
    private ColoredUniform Uniform;

    public ColoredPlane()
    {
        if (!AreVerticesGenerated) SetVertices();
        Uniform = new ColoredUniform();
    }

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

        Static_Vao = new VertexArrayObject();
        Static_Vbo = new BufferObject<float>(vertices, BufferTarget.ArrayBuffer);

        var layout = new VertexBufferLayout();
        layout.PushFloat(3); // xyz vertex coords
        Static_Vao.AddBuffer(Static_Vbo, layout);

        Static_Ebo = new BufferObject<uint>(indices, BufferTarget.ElementArrayBuffer);
        AreVerticesGenerated = true;
    }

    public override void Render(Camera camera)
    {
        if (Shader == null) return;

        Static_Vao.Bind();
        Static_Ebo.Bind();
        Shader.Use();
        SetShaderUniforms(camera);

        GL.DrawElements(PrimitiveType.Triangles, Static_Ebo.GetCount(), DrawElementsType.UnsignedInt, 0);
        base.Render(camera);
    }

    public override void SetShaderUniforms(Camera camera)
    {
        Uniform.Color = Color;
        Uniform.BorderRadiusPx = BorderRadius;

        Uniform.ScalePx = Scale.X;
        Uniform.AspectRatio = Scale.X / Scale.Y;
        Uniform.Model = Model;
        Uniform.Projection = camera.GetVPMatrix();

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
}