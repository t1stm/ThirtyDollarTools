# Textures

The texture layer wraps `glTexImage2D` / `glTexSubImage2D` in `GPUTexture`, infers OpenGL pixel formats from ImageSharp pixel types via `UploadInfoProvider<TPixel>`, and packs many small images into one larger texture using `GuillotineAtlas` + `GPUTextureAtlas`.

> Source: `Sundex/Sundex.Engine/Renderer/Textures/`.

## `GPUTexture`

```csharp
public class GPUTexture : IBindable
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public InternalFormat InternalFormat { get; set; } = InternalFormat.Rgba8;
    public MipmapMode     MipmapMode     { get; set; } = MipmapMode.Enabled;

    protected Queue<Action> UploadQueue { get; } = [];
    public int Handle { get; private set; }
    public BufferState BufferState { get; private set; } = BufferState.PendingCreation;

    public void Bind();
    public void Create();
    public void UploadBlankTextureToGPU();
    public unsafe void QueueUploadToGPU<TPixel>(ImageFrame<TPixel> frame, Rectangle? rect = null);
}
```

### Lazy creation + lazy upload

`Bind()` does *both* lazy creation and lazy upload draining:

```csharp
public void Bind()
{
    if (BufferState.HasFlag(BufferState.Failed))         throw ...;
    if (BufferState.HasFlag(BufferState.PendingCreation)) Create();
    GL.BindTexture(TextureTarget.Texture2d, Handle);
    if (BufferState.HasFlag(BufferState.PendingUpload)) UploadToGPU();
}
```

`Create` allocates a handle, uploads a blank `Width × Height` block to lock in the storage at `InternalFormat`, then sets the texture parameters appropriate to the [mipmap mode](#mipmapmode). After this, the texture is *guaranteed* to have storage so `glTexSubImage2D` calls always have somewhere to land.

### Upload queue

`QueueUploadToGPU` enqueues a closure that pins the image frame's pixel memory and calls `glTexSubImage2D`:

```csharp
UploadQueue.Enqueue(() =>
{
    if (!frame.DangerousTryGetSinglePixelMemory(out var pixelMemory))
        throw new Exception("Unable to get pixel memory.");

    var handle    = pixelMemory.Pin();
    var pixelInfo = UploadInfoProvider<TPixel>.UploadInfo;

    GL.TexSubImage2D(TextureTarget.Texture2d, 0,
        rect?.X ?? 0, rect?.Y ?? 0,
        rect?.Width ?? Width, rect?.Height ?? Height,
        pixelInfo.Format, pixelInfo.Type, handle.Pointer);
});
BufferState |= BufferState.PendingUpload;
```

The queue is drained on the next `Bind()`. Once drained, `SetTexParams()` is re-run if any uploads happened — needed because `GenerateMipmap` has to follow upload, not precede it.

### `MipmapMode`

```csharp
public enum MipmapMode { Enabled, Disabled }
```

| Mode | Min filter | Mag filter | Wrap | Mipmaps |
|---|---|---|---|---|
| `Enabled` | `LinearMipmapLinear` | `Linear` | `ClampToEdge` | Yes — `GenerateMipmap` after upload |
| `Disabled` | `Linear` | `Linear` | `ClampToEdge` | No — `MaxLevel = 0` |

`MaxLevel` for `Enabled` is `floor(log2(max(W, H)))` — i.e. the deepest meaningful mip.

## `UploadInfoProvider<TPixel>`

```csharp
public static class UploadInfoProvider<TPixel> where TPixel : unmanaged, IPixel, IPixel<TPixel>
{
    public static PixelUploadInfo UploadInfo { get; } = Resolve();
}
```

A static-generic cache that maps an ImageSharp `IPixel` type to the OpenGL `(PixelFormat, PixelType)` tuple. `Resolve()` is one big `switch` covering common pixel layouts:

| ImageSharp type | OpenGL format | OpenGL type |
|---|---|---|
| `Rgba32`, `Bgra32` | `Rgba` / `Bgra` | `UnsignedByte` |
| `Rgb24`, `Bgr24` | `Rgb` / `Bgr` | `UnsignedByte` |
| `Rgba64`, `Rgb48` | `Rgba` / `Rgb` | `UnsignedShort` |
| `RgbaVector` | `Rgba` | `Float` |
| `HalfVector4`, `HalfVector2`, `HalfSingle` | `Rgba` / `Rg` / `Red` | `HalfFloat` |
| `Rgba1010102` | `RgbaInteger` | `UnsignedInt1010102` |
| `L8`, `L16`, `A8` | `Red` / `Alpha` | `UnsignedByte`/`UnsignedShort` |

Static-in-generic-class is the dispatch trick: `UploadInfoProvider<Rgba32>.UploadInfo` is a separate static field from `UploadInfoProvider<RgbaVector>.UploadInfo`. The C# runtime instantiates one type per generic argument used, and each instantiation runs `Resolve()` exactly once. No reflection at the call site, no per-call dispatch.

The comment in source — *"This will keep one struct allocated for each TPixel that is used in the application but that isn't a problem"* — acknowledges the trade-off explicitly.

## Atlases

The atlas system pairs a 2D bin-packing algorithm with a `GPUTexture`.

### `IAtlas`

```csharp
public interface IAtlas
{
    int Width { get; }
    int Height { get; }
    bool CanFit(int width, int height);
    bool IsFull();
    int  GetRemainingArea();
    int  GetUsedArea();
    float GetUsagePercentage();
    Rectangle AddImage(string imageID, ImageFrame image);
    bool RemoveImage(string imageID);
}
```

The CPU-side bookkeeping interface — no GL calls.

### `GuillotineAtlas`

A guillotine bin packer: each placement splits the host free rectangle along one of two axes; merging recombines free rectangles whenever an image is removed. Best-fit scoring with a tiebreak:

```csharp
private readonly record struct PlacementScore(int AreaWaste, int MaxSideLeftover);
```

Lowest area waste wins; lowest `max(host.W - placed.W, host.H - placed.H)` breaks ties. Optional `_allowRotation` tries each candidate rotated as well.

Splitting picks orientation by the **shorter leftover axis heuristic**: split vertically if `host.W - placed.W <= host.H - placed.H`, horizontally otherwise. After every add/remove, free rectangles are pruned (drop any rectangle fully contained in another) and merged (combine same-row or same-column neighbours).

`Padding` (default 2 px) is added on every side before placement and trimmed off again before returning the user-visible rectangle. This avoids texel bleed at sample boundaries.

ID lookup uses a `Dictionary<string, Rectangle>` with `GetAlternateLookup<ReadOnlySpan<char>>` so callers can look up by stack-allocated keys.

The whole structure is `[JsonInclude]`-annotated so it round-trips through `System.Text.Json` — that is what enables [atlas caching](../Asset%20Management.md#the-cache) (see below).

### `GPUTextureAtlas`

Pairs `GuillotineAtlas` with a `GPUTexture`:

```csharp
public class GPUTextureAtlas(int width, int height,
    InternalFormat internalFormat = InternalFormat.Rgba8,
    MipmapMode mipmapMode = MipmapMode.Enabled)
{
    public required string AtlasID { get; init; }
    public GPUTexture Texture { get; set; }
    public GuillotineAtlas Atlas { get; set; }

    public void AddTexture<TPixel>(string name, ImageFrame<TPixel> image)
        where TPixel : unmanaged, IPixel, IPixel<TPixel>
    {
        var rectangle = Atlas.AddImage(name, image);
        Texture.QueueUploadToGPU(image, rectangle);
    }

    public void LoadFromCache(AssetProvider assetProvider);
    public void Bind();
}
```

Add image → ask atlas for a rectangle → enqueue an upload to that rectangle. The `AddImage` happens on the CPU side, the actual `glTexSubImage2D` happens on the next `Bind`.

### Atlas caching

`LoadFromCache` queries the [`CacheProvider`](../Asset%20Management.md#the-cache) for two artefacts keyed by `AtlasID`:

- `Atlas_Texture<id>` — the rendered atlas image, decoded with `Image.Load<Rgba32>` or `Image.Load<RgbaVector>` depending on the `InternalFormat`.
- `Atlas_Lookup<id>` — the `GuillotineAtlas` JSON, deserialised back into a fully-populated bin-pack state.

If both exist the texture is uploaded directly and the atlas's used-rectangle dictionary is restored — bypassing per-glyph rasterisation. This is what lets [TextProvider](../Text%20Rendering/Text%20Rendering.md) start instantly on second launch even though MSDF generation is expensive.

Only `Rgba8` and `Rgba32f` formats can be cached — the other internal formats either don't have a portable on-disk representation or aren't used for atlases.

## Threading

- **`AddTexture` / `AddImage`** can be called off-thread (the atlas takes a `lock (_usedByImageID)`), but the actual GL upload happens on the GL thread via `Bind()`.
- **`Bind()` and `Create()` are GL-thread-only.**
- **Disposal** of a `GPUTexture` should enqueue `DeleteType.Texture` on the [DeleteQueue](Queues.md) (the texture class itself doesn't currently expose `Dispose` directly, but the pattern matches `GLBuffer`).

## Related

- [TextProvider](../Text%20Rendering/Text%20Rendering.md) uses a single `GPUTextureAtlas` (2048×2048, `Rgba32f`) for all glyph MSDFs.
- [CacheProvider](../Asset%20Management.md#the-cache) is what makes atlas caching possible.
- [GLQuad](Buffers.md) supplies the geometry that gets textured at draw time.
