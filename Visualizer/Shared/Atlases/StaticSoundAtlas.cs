using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Shaders;
using Sundex.Engine.Renderer.Textures;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sundex.Engine.Renderer.Buffers;
using Sundex.Engine.Renderer.Textures.Atlases;

namespace Shared.Atlases;

/// <summary>
///     An atlas of single-frame sound images. Each image gets a slot in a UV table that
///     <see cref="Bind" /> hands the Static shader as a uniform block, so an instance carries a
///     2-byte slot index instead of its 16 bytes of UVs.
/// </summary>
public class StaticSoundAtlas(
    int width,
    int height,
    TextureFormat format = TextureFormat.Rgba8Unorm)
    : GPUTextureAtlas(width, height, format)
{
    /// <summary>
    ///     How many images one atlas holds: the length of <c>u_UVs</c> in Static.vert. 1024 vec4s fill
    ///     the 16 KB uniform block every GL 3.3 driver allows.
    /// </summary>
    public const int MaxImages = 1024;

    private const int UVBinding = 2;
    private const int TextureBinding = 3;
    public static ShaderLayout Layout { get; } = new(ShaderLayout.View,
        new ShaderBinding("UVs", UVBinding, ShaderBindingType.UniformBuffer),
        new ShaderBinding("u_Texture", TextureBinding, ShaderBindingType.Texture));

    private readonly Dictionary<string, ushort> _indices = [];
    private readonly Lock _lock = new();
    private readonly GpuBuffer<Vector4> _uvBuffer = new(BufferUsage.Uniform);

    // The whole block is uploaded, as a buffer smaller than the block it backs is undefined behavior.
    private readonly Vector4[] _uvs = new Vector4[MaxImages];
    private bool _uvsDirty = true;

    public bool IsFull
    {
        get
        {
            lock (_lock)
            {
                return _indices.Count >= MaxImages;
            }
        }
    }

    /// <summary>Places <paramref name="image" /> in the atlas and gives it a UV slot.</summary>
    public void AddSound(string soundName, ImageFrame<Rgba32> image)
    {
        AddTexture(soundName, image);
        var rectangle = Atlas.GetImageRectangle(soundName);

        lock (_lock)
        {
            if (!_indices.TryGetValue(soundName, out var index))
                _indices.Add(soundName, index = (ushort)_indices.Count);

            _uvs[index] = QuadUV.FromRectangle(rectangle, Width, Height).UV;
            _uvsDirty = true;
        }
    }

    public bool TryGetSound(ReadOnlySpan<char> soundName, out Rectangle reference, out ushort uvIndex)
    {
        reference = Atlas.GetImageRectangle(soundName);

        lock (_lock)
        {
            return _indices.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(soundName, out uvIndex) &&
                   !reference.IsEmpty;
        }
    }

    /// <summary>Binds the texture and the UV table, uploading slots added since the last bind.</summary>
    public void Bind(ICommandList commands)
    {
        commands.SetTexture(TextureBinding, Texture, Texture.DefaultSampler);

        lock (_lock)
        {
            if (_uvsDirty)
            {
                _uvBuffer.SetData(_uvs);
                _uvsDirty = false;
            }
        }

        commands.SetUniformBuffer(UVBinding, _uvBuffer);
    }
}
