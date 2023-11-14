using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects.Planes;

public class ColoredPlane : Renderable
{
    private static bool AreVerticesGenerated;
    private static VertexArrayObject<float> Static_Vao = null!;
    private static BufferObject<float> Static_Vbo = null!;
    private static BufferObject<uint> Static_Ebo = null!;

    public float BorderRadius = 0f;
    
    public ColoredPlane(Vector4 color, Vector3 position, Vector2 width_height)
    {
        _position = new Vector3(position);
        _scale = new Vector3(width_height.X, width_height.Y, 0);
        
        if (!AreVerticesGenerated) SetVertices();
        
        Vao = Static_Vao;
        Vbo = Static_Vbo;
        Ebo = Static_Ebo;
        
        Shader = new Shader("ThirtyDollarVisualizer.Assets.Shaders.colored.vert", "ThirtyDollarVisualizer.Assets.Shaders.colored.frag");
        Color = color;
    }

    public ColoredPlane(Vector4 color, Vector3 position, Vector2 width_height, Shader? shader) : this(color, position, width_height)
    {
        Shader = shader ?? Shader;
    }

    private void SetVertices()
    {
        lock (LockObject)
        {
            var (x, y, z) = (0f, 0f, 0);
            var (w, h) = (1f, 1f);
        
            var vertices = new[] {
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
        Shader.SetUniform("u_Color", Color);
        Shader.SetUniform("u_BorderRadiusPx", BorderRadius);
        Shader.SetUniform("u_PositionPx", _position.Xy + _translation.Xy);
        Shader.SetUniform("u_ScalePx", _scale.Xy);
        
        Shader.SetUniform("u_Model", Model);
        Shader.SetUniform("u_Projection", camera.GetProjectionMatrix());
        if (camera is DollarStoreCamera camera_30)
        {
            Shader.SetUniform("u_Time", camera_30.GetRunningTime());
        }
    }

    public override void Dispose()
    {
        lock (LockObject)
        {
            Shader.Dispose();
        }
    }
}