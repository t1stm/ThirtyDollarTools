using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Extensions;
using Sundex.Engine.Asset_Management.Types.Shader;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Renderer.Debug;
using Sundex.Engine.Renderer.Shaders;
using Sundex.Engine.Renderer.Textures;
using Sundex.Engine.Renderer.Textures.Atlases;
using Sundex.Engine.Text.Fonts;

namespace Sundex.Engine.Text;

[PreloadGraphicsContext]
public class TextProvider(IAssetProvider provider, IFontProvider fontProvider, string fontName)
    : IGamePreloadable, ITextProvider
{
    private static Shader _shader = null!;

    public readonly GPUTextureAtlas TextAtlas = new(2048, 2048, InternalFormat.Rgba32f, MipmapMode.Disabled)
    {
        AtlasID = "TextAtlas_" + fontName.Replace(' ', '_')
    };

    public IAssetProvider AssetProvider { get; } = provider;

    public static void Preload(AssetProvider assetProvider)
    {
        _shader = assetProvider.ShaderPool.GetOrLoad("Assets/Shaders/Text/Batched", provider =>
            new Shader(provider, provider.LoadShaders(
                ShaderInfo.CreateFromUnknownStorage(ShaderType.VertexShader, "Assets/Shaders/Text/Batched.vert"),
                ShaderInfo.CreateFromUnknownStorage(ShaderType.FragmentShader, "Assets/Shaders/Text/Batched.frag")))
        );
    }

    public IGlyphProvider GlyphProvider { get; } = new GlyphProvider(fontProvider, fontName);

    public (Vector4, TextAlignmentData) GetTextCharacterRect(ReadOnlySpan<char> character)
    {
        lock (TextAtlas)
        {
            var characterUV = TextAtlas.Atlas.GetImageRectangle(character);
            if (!characterUV.IsEmpty)
                return ((characterUV.X, characterUV.Y, characterUV.Width, characterUV.Height),
                    GlyphProvider.GetSizingData(character));

            AddCharacter(character);
            characterUV = TextAtlas.Atlas.GetImageRectangle(character);

            return ((characterUV.X, characterUV.Y, characterUV.Width, characterUV.Height),
                GlyphProvider.GetSizingData(character));
        }
    }

    /// <summary>
    ///     Binds the text atlas to the OpenGL context and activates the shader program.
    /// </summary>
    /// <param name="camera">The camera that contains the VP matrix to use in the shader.</param>
    public void BindAndSetUniforms(Camera camera)
    {
        lock (TextAtlas)
        {
            TextAtlas.Bind();

            _shader.Use();

            _shader.SetUniform("uVPMatrix", camera.GetVPMatrix());
            _shader.SetUniform("uPxRange", Fonts.GlyphProvider.GlyphSize * Fonts.GlyphProvider.MsdfRange);

            RenderMarker.Debug("Bound Text Atlas and Set Uniforms: ", TextAtlas.AtlasID, MarkerType.Hidden);
        }
    }

    public float TextureWidth
    {
        get
        {
            lock (TextAtlas)
            {
                return TextAtlas.Width;
            }
        }
    }

    public float TextureHeight
    {
        get
        {
            lock (TextAtlas)
            {
                return TextAtlas.Height;
            }
        }
    }

    private void AddCharacter(ReadOnlySpan<char> character)
    {
        lock (TextAtlas)
        {
            var image = GlyphProvider.GetGlyph(character);
            TextAtlas.AddTexture(character.ToString(), image.Frames.RootFrame);
        }
    }
}