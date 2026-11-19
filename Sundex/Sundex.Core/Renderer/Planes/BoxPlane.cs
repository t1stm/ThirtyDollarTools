using System.Runtime.InteropServices;
using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

using Sundex.Core;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Extensions;
using Sundex.Engine.Asset_Management.Types.Shader;
using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Renderer.Data_Buffers;
using Sundex.Engine.Renderer.Queues;
using Sundex.Engine.Renderer.Shaders;

namespace Sundex.Core.Renderer.Planes;

/// <summary>
///     The plane behind every panel: what ColoredPlane is, plus a per-side border, a
///     radial glow and a frosted backdrop. Follows ColoredPlane's own recipe, since a renderable
///     with a shader of its own has to bring its own quad, uniform block and preload.
/// </summary>
[PreloadGraphicsContext]
public sealed class BoxPlane : Renderable, IGamePreloadable, IBorderRadius
{
    private static DeleteQueue _deleteQueue = null!;
    private static Shader _shader = null!;
    private static VertexArrayObject? _vao;
    private static GLBuffer<Uniform>? _uniforms;
    private static int _backdropTexture;

    public Vector4 BorderColor;
    public Vector4 BorderWidths;
    public Vector4 GlowColor;
    public Vector4 Glow = new(0.5f, 0.5f, 1, 1);

    /// <summary>Mip level of the copied framebuffer to draw behind the fill; negative for none.</summary>
    public float BackdropLod = -1;

    /// <summary>Physical pixels per UI unit, needed to find this box in the framebuffer.</summary>
    public Vector2 PixelScale = Vector2.One;

    public override Shader Shader
    {
        get => _shader;
        set => _shader = value ?? throw new ArgumentNullException(nameof(value));
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

    public float BorderRadius { get; set; }

    [UsedImplicitly]
    public static void Preload(AssetProvider assetProvider)
    {
        _deleteQueue = assetProvider.DeleteQueue;
        _shader = assetProvider.ShaderPool.GetOrLoad("Assets/Shaders/Box", provider =>
            new Shader(provider, provider.LoadShaders(
                ShaderInfo.CreateFromUnknownStorage(ShaderType.VertexShader, "Assets/Shaders/Box/box.vert"),
                ShaderInfo.CreateFromUnknownStorage(ShaderType.FragmentShader, "Assets/Shaders/Box/box.frag"))));
    }

    public override void Render(Camera camera)
    {
        if (_vao == null)
        {
            _vao = new VertexArrayObject();
            var layout = new VertexBufferLayout();
            layout.PushFloat(3);
            _vao.AddBuffer(GLQuad.VBOWithoutUV, layout);
            _vao.SetIndexBuffer(GLQuad.EBO);
        }

        var uvRect = new Vector4(0, 0, 1, 1);
        if (BackdropLod >= 0) uvRect = CopyBackdrop();

        _vao.Bind();
        _vao.Update();
        Shader.Use();

        Span<Uniform> data =
        [
            new Uniform
            {
                Model = Model,
                Projection = camera.GetVPMatrix(),
                Color = Color,
                BorderColor = BorderColor,
                Border = BorderWidths,
                ScaleRadiusLod = new Vector4(Scale.X, Scale.Y, BorderRadius, BackdropLod),
                GlowColor = GlowColor,
                Glow = Glow,
                UvRect = uvRect
            }
        ];
        _uniforms ??= new GLBuffer<Uniform>(_deleteQueue, BufferTarget.UniformBuffer);
        _uniforms.Dangerous_SetBufferData(data);
        GL.BindBufferBase(BufferTarget.UniformBuffer, 0, _uniforms.Handle);

        GL.DrawElements(PrimitiveType.Triangles, GLQuad.EBO.Capacity, DrawElementsType.UnsignedInt, 0);
    }

    /// <summary>
    ///     What backdrop-blur needs and Sundex has no pass for: the frame so far, under this box,
    ///     as a mipmapped texture. The mips are the blur, cheap and a little boxy at a high level.
    /// </summary>
    private Vector4 CopyBackdrop()
    {
        if (_backdropTexture == 0)
        {
            _backdropTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _backdropTexture);
            GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear);
            GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                (int)TextureWrapMode.ClampToEdge);
            GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                (int)TextureWrapMode.ClampToEdge);
        }

        var viewport = new int[4];
        GL.GetInteger(GetPName.Viewport, viewport);
        var height = viewport[3];
        var x = Math.Max(0, (int)(Position.X * PixelScale.X));
        var boxWidth = Math.Max(1, (int)(Scale.X * PixelScale.X));
        var boxHeight = Math.Max(1, (int)(Scale.Y * PixelScale.Y));
        var y = Math.Max(0, height - (int)(Position.Y * PixelScale.Y) - boxHeight);

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _backdropTexture);
        GL.CopyTexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, x, y, boxWidth, boxHeight, 0);
        GL.GenerateMipmap(TextureTarget.Texture2D);
        return new Vector4(0, 0, 1, 1);
    }

    [StructLayout(LayoutKind.Explicit, Size = 240)]
    private struct Uniform
    {
        [FieldOffset(0)] public Matrix4 Model;
        [FieldOffset(64)] public Matrix4 Projection;
        [FieldOffset(128)] public Vector4 Color;
        [FieldOffset(144)] public Vector4 BorderColor;
        [FieldOffset(160)] public Vector4 Border;
        [FieldOffset(176)] public Vector4 ScaleRadiusLod;
        [FieldOffset(192)] public Vector4 GlowColor;
        [FieldOffset(208)] public Vector4 Glow;
        [FieldOffset(224)] public Vector4 UvRect;
    }
}
