using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Renderer.Cameras;
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
        _shader = assetProvider.ShaderPool.GetOrLoad("Assets/Shaders/Text/Batched");
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

    /// <inheritdoc />
    public void Warm(string characters)
    {
        // Parallel because MsdfFont hands its scratch out of a concurrent pool, so the
        // generators genuinely do not block each other. Measured on the printable ASCII
        // set: 0.94 ms/glyph serial against 0.28 ms/glyph across 16 cores - sub-linear,
        // since the outline work per glyph is short, but a real 3.4x.
        // Half the machine: see the note in SundexContext.PrecompileLogic. The caller is
        // a screen that is still drawing while this runs.
        Parallel.ForEach(characters.Distinct(),
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) },
            character =>
            {
                // The atlas is the authority on what has already been paid for. Checked
                // here rather than in the glyph provider, which does not know about it.
                lock (TextAtlas)
                {
                    if (!TextAtlas.Atlas.GetImageRectangle(character.ToString()).IsEmpty) return;
                }

                GlyphProvider.Warm([character]);
            });
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