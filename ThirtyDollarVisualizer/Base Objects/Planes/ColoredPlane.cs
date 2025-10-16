using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Assets;
using ThirtyDollarVisualizer.Base_Objects.Planes.Uniforms;
using ThirtyDollarVisualizer.Renderer;
using ThirtyDollarVisualizer.Renderer.Abstract;
using ThirtyDollarVisualizer.Renderer.Attributes;
using ThirtyDollarVisualizer.Renderer.Buffers;
using ThirtyDollarVisualizer.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Base_Objects.Planes;

[PreloadGL]
public class ColoredPlane : Renderable, IGLPreloadable
{
    private static bool _areVerticesGenerated;
    private static VertexArrayObject _staticVAO = null!;
    private static GLBuffer<ColoredUniform>? _uniformBuffer;

    private Lazy<Shader> _shader = new(() => ShaderPool.GetNamedShader("colored_plane"), LazyThreadSafetyMode.None);

    private ColoredUniform _uniform;
    public float BorderRadius;

    [UsedImplicitly]
    public static void Preload()
    {
        ShaderPool.PreloadShader("colored_plane", static () => Shader.NewVertexFragment(
                Asset.Embedded("Shaders/colored.vert"),
                Asset.Embedded("Shaders/colored.frag")
            ));
    }
    
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

        _uniformBuffer ??= new GLBuffer<ColoredUniform>(BufferTarget.UniformBuffer);
        _uniformBuffer.DangerousGLThread_SetBufferData(span);
        GL.BindBufferBase(BufferTarget.UniformBuffer, 0, _uniformBuffer.Handle);
    }
}