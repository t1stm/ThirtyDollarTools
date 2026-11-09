using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sundex.Engine.Renderer.Data_Buffers;
using Sundex.Engine.Renderer.Queues;
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
    DeleteQueue deleteQueue,
    InternalFormat internalFormat = InternalFormat.Rgba8)
    : GPUTextureAtlas(width, height, internalFormat)
{
    /// <summary>
    ///     How many images one atlas holds: the length of <c>u_UVs</c> in Static.vert. 1024 vec4s fill
    ///     the 16 KB uniform block every GL 3.3 driver allows.
    /// </summary>
    public const int MaxImages = 1024;

    /// <summary>The uniform block binding Static.vert reads its UVs from (a block's default).</summary>
    private const int UVBinding = 0;

    private readonly Dictionary<string, ushort> _indices = [];
    private readonly Lock _lock = new();
    private readonly GLBuffer<Vector4> _uvBuffer = new(deleteQueue, BufferTarget.UniformBuffer);

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
    public override void Bind()
    {
        base.Bind();

        lock (_lock)
        {
            if (_uvsDirty)
            {
                _uvBuffer.Dangerous_SetBufferData(_uvs);
                _uvsDirty = false;
            }
        }

        GL.BindBufferBase(BufferTarget.UniformBuffer, UVBinding, _uvBuffer.Handle);
    }
}
