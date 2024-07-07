using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Objects.Planes.Uniforms;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects.Planes;

public class ColoredPlane : Renderable
{
    private static bool AreVerticesGenerated;
    private static VertexArrayObject<float> Static_Vao = null!;
    private static BufferObject<float> Static_Vbo = null!;
    private static BufferObject<uint> Static_Ebo = null!;
    private BufferObject<ColoredUniform>? UniformBuffer;
    
    public float BorderRadius;
    private ColoredUniform Uniform;

    public ColoredPlane(Vector4 color, Vector3 position, Vector3 scale, float border_radius = 0f)
    {
        _position = position;
        _scale = scale;

        if (!AreVerticesGenerated) SetVertices();

        Vao = Static_Vao;
        Vbo = Static_Vbo;
        Ebo = Static_Ebo;

        Shader = new Shader("ThirtyDollarVisualizer.Assets.Shaders.colored.vert",
            "ThirtyDollarVisualizer.Assets.Shaders.colored.frag");
        Color = color;

        Uniform = new ColoredUniform();
        BorderRadius = border_radius;
    }

    public ColoredPlane(Vector4 color, Vector3 position, Vector3 scale, Shader? shader) : this(color, position, scale)
    {
        Shader = shader ?? Shader;
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
            if (Ebo == null || Vao == null) return;

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
        Uniform.PositionPx = _position + _translation;
        Uniform.ScalePx = _scale;
        Uniform.Model = Model;
        Uniform.Projection = camera.GetProjectionMatrix();
        
        Span<ColoredUniform> span = stackalloc ColoredUniform[] { Uniform };

        if (UniformBuffer is null)
        {
            UniformBuffer = new BufferObject<ColoredUniform>(span, BufferTarget.UniformBuffer, BufferUsageHint.StreamDraw);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, UniformBuffer.Handle);
        }
        else UniformBuffer.SetBufferData(span, BufferTarget.UniformBuffer, BufferUsageHint.StreamDraw);
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, UniformBuffer.Handle);
    }

    public override void Dispose()
    {
    }
}