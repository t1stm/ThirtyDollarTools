using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Renderer.Shaders;
using Sundex.Engine.Renderer.Textures;
using Sundex.Engine.Text.Fonts;
using Sundex.Slug;

namespace Sundex.Engine.Text;

[PreloadGraphicsContext]
public class TextProvider(IAssetProvider provider, IFontProvider fontProvider, string fontName)
    : IGamePreloadable, ITextProvider
{
    private static Shader _shader = null!;

    private readonly GlyphProvider _glyphProvider = new(fontProvider, fontName);

    // ponytail: one texture pair per provider, so two providers over one font upload it
    // twice (~7 MiB each). Cache them per SlugFontData if that ever matters.
    private GPUTexture? _curves;
    private GPUTexture? _bands;

    public IAssetProvider AssetProvider { get; } = provider;

    public static void Preload(AssetProvider assetProvider)
    {
        _shader = assetProvider.ShaderPool.GetOrLoad("Assets/Shaders/Text/Batched");
    }

    public IGlyphProvider GlyphProvider => _glyphProvider;

    /// <summary>
    ///     Binds the curve texture to unit 0 and the band texture to unit 1, and activates the
    ///     shader program. Unit 0 is left active, since everything else binds there.
    /// </summary>
    /// <param name="camera">The camera that contains the VP matrix to use in the shader.</param>
    public void BindAndSetUniforms(Camera camera)
    {
        if (_curves is null || _bands is null)
        {
            var data = _glyphProvider.SlugData;
            _curves = CreateTexture(data.Curves, data.CurveRows);
            _bands = CreateTexture(data.Bands, data.BandRows);
        }

        GL.ActiveTexture(TextureUnit.Texture1);
        _bands.Bind();
        GL.ActiveTexture(TextureUnit.Texture0);
        _curves.Bind();

        _shader.Use();
        _shader.SetUniform("uVPMatrix", camera.GetVPMatrix());
        _shader.SetUniform("uCurves", 0);
        _shader.SetUniform("uBands", 1);

        RenderMarker.Debug("Bound Slug Textures and Set Uniforms: ", fontName, MarkerType.Hidden);
    }

    private static GPUTexture CreateTexture(float[] texels, int rows)
    {
        var texture = new GPUTexture
        {
            Width = SlugFontData.TextureWidth,
            Height = rows,
            InternalFormat = InternalFormat.Rgba32f,
            MipmapMode = MipmapMode.Disabled
        };

        // The upload needs the pixels in one block, and a band texture (~5 MiB) is past the size
        // ImageSharp otherwise splits into several, unless the app set this globally.
        var configuration = Configuration.Default.Clone();
        configuration.PreferContiguousImageBuffers = true;

        var image = Image.LoadPixelData<RgbaVector>(configuration, MemoryMarshal.Cast<float, RgbaVector>(texels),
            SlugFontData.TextureWidth, rows);
        texture.QueueUploadToGPU(image.Frames.RootFrame);
        return texture;
    }
}
