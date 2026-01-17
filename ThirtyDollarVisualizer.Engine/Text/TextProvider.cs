using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Engine.Asset_Management;
using ThirtyDollarVisualizer.Engine.Asset_Management.Extensions;
using ThirtyDollarVisualizer.Engine.Asset_Management.Types.Shader;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Attributes;
using ThirtyDollarVisualizer.Engine.Renderer.Cameras;
using ThirtyDollarVisualizer.Engine.Renderer.Debug;
using ThirtyDollarVisualizer.Engine.Renderer.Shaders;
using ThirtyDollarVisualizer.Engine.Renderer.Textures.Atlases;
using ThirtyDollarVisualizer.Engine.Text.Fonts;

namespace ThirtyDollarVisualizer.Engine.Text;

[PreloadGraphicsContext]
public class TextProvider(AssetProvider provider, FontProvider fontProvider, string fontName)
    : IGamePreloadable
{
    private static Shader _shader = null!;

    public static void Preload(AssetProvider assetProvider)
    {
        _shader = assetProvider.ShaderPool.GetOrLoad("Assets/Shaders/Text/Batched", provider =>
            new Shader(provider, provider.LoadShaders(
                ShaderInfo.CreateFromUnknownStorage(ShaderType.VertexShader, "Assets/Shaders/Text/Batched.vert"),
                ShaderInfo.CreateFromUnknownStorage(ShaderType.FragmentShader, "Assets/Shaders/Text/Batched.frag")))
        );
    }

    public readonly GPUTextureAtlas TextAtlas = new(2048, 2048, InternalFormat.Rgba32f)
    {
        AtlasID = "TextAtlas_" + fontName.Replace(' ', '_')
    };

    public readonly GlyphProvider GlyphProvider = new(fontProvider, fontName);
    public AssetProvider AssetProvider { get; } = provider;

    private void AddCharacter(ReadOnlySpan<char> character)
    {
        lock (TextAtlas)
        {
            var image = GlyphProvider.GetGlyph(character);
            TextAtlas.AddTexture(character.ToString(), image.Frames.RootFrame);
        }
    }

    public (Vector4, TextAlignmentData) GetTextCharacterRect(ReadOnlySpan<char> character)
    {
        lock (TextAtlas)
        {
            var characterUV = TextAtlas.Atlas.GetImageRectangle(character);
            if (!characterUV.IsEmpty)
                return ((characterUV.X, characterUV.Y, characterUV.Width, characterUV.Height), GlyphProvider.GetSizingData(character));

            AddCharacter(character);
            characterUV = TextAtlas.Atlas.GetImageRectangle(character);

            return ((characterUV.X, characterUV.Y, characterUV.Width, characterUV.Height), GlyphProvider.GetSizingData(character));
        }
    }

    /// <summary>
    /// Binds the text atlas to the OpenGL context and activates the shader program.
    /// </summary>
    /// <param name="camera">The camera that contains the VP matrix to use in the shader.</param>
    /// <param name="color">The color of the text.</param>
    public void BindAndSetUniforms(Camera camera, Vector4 color)
    {
        lock (TextAtlas)
        {
            TextAtlas.Bind();

            _shader.Use();

            _shader.SetUniform("uVPMatrix", camera.GetVPMatrix());
            _shader.SetUniform("uOutputColor", color);
            _shader.SetUniform("uPxRange", GlyphProvider.GlyphSize * GlyphProvider.MsdfRange);

            RenderMarker.Debug("Bound Text Atlas and Set Uniforms: ", TextAtlas.AtlasID, MarkerType.Hidden);
        }
    }
}