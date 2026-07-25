# Asset Management

The asset pipeline is built around one central type — `AssetProvider` — and a small number of generic abstractions (`IAssetLoader<TReturn, TCreate>`, `ILoadableAsset<TReturn, TCreate>`, `IAssetMetadata`) that let any asset type plug in without modifying the provider. There are no string-keyed asset registries or dynamic lookups: every load goes through compile-time generics.

> Source: `Sundex/Sundex.Engine/Asset Management/`

## The provider

`AssetProvider` (`AssetProvider.cs`) is constructed once by [[Entrypoint|Game]] and exposes:

| Property | Type | Purpose |
|---|---|---|
| `Logger` | `ILogger` | `ForContext<AssetProvider>()` |
| `AssetAssemblies` | `Assembly[]` | Assemblies scanned for embedded resources |
| `ShaderPool` | `ShaderPool` | Named shader cache + preload queue |
| `DeleteQueue` | [[Renderer/Queues|DeleteQueue]] | Deferred GPU-resource deletion |
| `CacheProvider` | `CacheProvider` | Disk caching of decoded assets |
| `GLInfo` | `GLInfo` | Read-only GL capabilities |

Three operations form the entire surface area:

```csharp
public bool      Query<TReturn, TCreate>(TCreate createInfo);
public TReturn   Load<TReturn, TCreate>(TCreate createInfo);
public TMetadata Metadata<TMetadata, TCreate>(TCreate createInfo);
```

with two ergonomic overloads of `Load` for batch loading into a `Span<TReturn>` or returning a fresh `TReturn[]`.

`AssetProvider.Update()` runs once per tick from the GL thread and performs three maintenance operations:

```csharp
ShaderPool.UploadShadersToPreload();   // upload preload-queued shaders
DeleteQueue.ExecuteDeletes();          // run deferred GL.Delete*
CacheProvider.SaveQueuedAssets();      // flush enqueued cache writes to disk
```

## The two generic interfaces

Asset types opt in by implementing `ILoadableAsset<TReturn, TCreate>`:

```csharp
public interface ILoadableAsset<TReturn, TCreate>
{
    static abstract IAssetLoader<TReturn, TCreate> AssetLoader { get; }
}
```

The C# 11 `static abstract` member means each asset declares its own loader as a static property — no service locator, no DI container. `AssetProvider.Load<TReturn, TCreate>` simply calls `TReturn.AssetLoader.Load(createInfo, this)`.

The loader contract:

```csharp
public interface IAssetLoader<TReturn, TCreate>
{
    bool      Query(TCreate createInfo, AssetProvider assetProvider);
    TReturn   Load (TCreate createInfo, AssetProvider assetProvider);
    TReturn   Load (TCreate createInfo, AssetProvider assetProvider,
                    Func<TCreate, AssetProvider, TReturn> create);
    static abstract TReturn Create(TCreate createInfo, AssetProvider assetProvider);
}
```

`Query` is non-throwing and asks "could this be loaded?". `Load` produces the asset (possibly throwing), and the overload taking a `Func<TCreate, AssetProvider, TReturn>` lets callers inject a custom factory while still going through the same code path. `Create` is a static method — it's the canonical "make one from scratch" implementation.

## Storage locations

The generic asset stream type is `AssetStream` (`Types/Asset/AssetStream.cs`), produced by `AssetLoader` (`Types/Asset/AssetLoader.cs`) from an `AssetInfo`:

```csharp
public class AssetInfo
{
    public string Location;
    public StorageLocation Storage;
}

public enum StorageLocation
{
    Unknown,    // probe disk first, then assemblies
    Disk,       // File.OpenRead(Location)
    Assembly,   // Assembly.GetManifestResourceStream(Location)
    Network     // HttpClient.GetAsync(Location)
}
```

`StorageLocation.Unknown` is the most common path: `Query` returns true if the file exists on disk **or** as a manifest resource in any registered asset assembly, and `Create` tries disk first, falling back to `assetProvider.AssetAssemblies.GetManifestResourceStream(...)`. After resolution the `AssetInfo.Storage` field is rewritten to the concrete location, so subsequent uses of the same `AssetInfo` skip the probe.

`Disk` paths support glob patterns (e.g. `Assets/Sounds/something_*.wav`) — `AssetLoader` enumerates the directory and takes the first match. Useful for engineer / migration tools.

`Network` uses a `Lazy<HttpClient>` and is synchronous (`GetAwaiter().GetResult()`). It exists primarily for the `SampleHolder` style use case where you want to download once at startup.

## Concrete asset types

Each of these implements `ILoadableAsset<TReturn, TCreate>` and declares a static `AssetLoader`:

| Type | Folder | Create info | Output |
|---|---|---|---|
| `AssetStream` | `Types/Asset/` | `AssetInfo` | A `Stream` + resolved `AssetInfo` |
| `StringAsset` | `Types/String/` | `StringInfo` | UTF-8 decoded text |
| `Shader` | `Renderer/Shaders/` (loader in `Types/Shader/`) | `ShaderInfo` × 2 (vert + frag) | Compiled GL shader program |
| `TextureHolder` | `Types/Texture/` | `TextureInfo` | Decoded `Image<Rgba32>` (via [[#The cache]]) ready to upload |
| `CachedAsset` | `Types/Cache/` | `CachedInfo` | Raw `byte[]` from `~/.cache/...` |

Each `*Loader.cs` file follows the same shape: `Query` checks existence, `Create` reads the underlying stream (often by recursively `Load`ing an `AssetStream` first), and `Load` is `Load(info, provider, Create)`.

## Shaders and the `ShaderPool`

`ShaderPool` (`Helpers/ShaderPool.cs`) layers two extra concepts on top of the basic loader pattern:

1. **Named caching** — shaders are keyed by their location (the path without `.vert`/`.frag`) in a `Dictionary<string, Shader>`. Lookups use `GetAlternateLookup<ReadOnlySpan<char>>()` so callers can probe with stack-allocated names.
2. **Preload queue** — `PreloadShader(name, factory)` doesn't compile anything; it appends to `_shadersToPreload`. `UploadShadersToPreload()` (called from `AssetProvider.Update`) drains the queue *on the GL thread*. This is what `[PreloadGraphicsContext]` types call from their `static Preload(AssetProvider)` methods.

Two ways to load:

```csharp
// One-shot — compiles immediately on the calling thread
var shader = assetProvider.ShaderPool.GetOrLoad("Assets/Shaders/Quad");

// Preload — defer to next OnUpdateFrame
assetProvider.ShaderPool.PreloadShader("Quad",
    p => new Shader(p, p.LoadShaders(
        ShaderInfo.CreateFromUnknownStorage(ShaderType.VertexShader, "Assets/Shaders/Quad.vert"),
        ShaderInfo.CreateFromUnknownStorage(ShaderType.FragmentShader, "Assets/Shaders/Quad.frag"))));
```

`GetOrLoad(shaderLocation)` is a convenience for the standard `name.vert` + `name.frag` pair — it builds two `ShaderInfo`s with `StorageLocation.Unknown` and constructs a `Shader` directly. The `LoadShaders` extension lives in `Asset Management/Extensions/ShaderExtensions.cs`.

`Reload()` re-compiles every cached shader. Useful for hot-reload during development.

## The cache

`CacheProvider` (`CacheProvider.cs`) caches decoded assets to disk so subsequent runs can skip expensive decoding (e.g. JPEG → ARGB pixel buffer):

```csharp
// Probe
if (cacheProvider.TryLoadingCachedAsset(info, out var cached)) {
    UseRawBytes(cached.Data);
    return;
}

// Compute
var bytes = ExpensiveDecode(...);

// Enqueue write — flushed on next AssetProvider.Update
cacheProvider.EnqueueAssetToCache(info, bytes);
```

Internally:

- `_cachedAssets` is a `Queue<(CachedInfo, byte[])>` guarded by `lock (_cachedAssets)` — work-item pattern, not `ConcurrentQueue`, because writes are flushed in a single batch from `Update`.
- `CachedInfo.CacheID` is a stable string used to derive the on-disk filename via `CachedAssetLoader.GenerateAssetInfoBasedOnCacheID(cacheID)`.
- `SaveQueuedAssets()` writes everything synchronously inside the lock — this only runs from the main thread between frames, so the cost is bounded and predictable.

## Metadata

`IAssetMetadata<TMetadata, TCreate>` lets you query lightweight properties (existence, modified date, size) without opening a stream. `AssetMetadata` is the basic implementation:

```csharp
public struct AssetMetadata { public bool Found; public DateTime ModifiedDate; }

var meta = assetProvider.Metadata<AssetMetadata, AssetInfo>(info);
if (meta.ModifiedDate > _lastSeen) ReloadFromDisk();
```

For `StorageLocation.Disk`, `Metadata` calls `File.GetLastWriteTime`. For `Assembly` / `Network` / `Unknown` it returns `DateTime.UnixEpoch` — the comment in source says *"it's best to not overcomplicate things sometimes"*, i.e. those resources are treated as always-fresh.

The `allows ref struct` constraint on `Metadata<TMetadata, TCreate>` means metadata structs can be `ref struct`s and stack-allocated, avoiding heap pressure for hot paths.

## Embedded vs. file-on-disk

The project ships shaders, fonts, and default sounds as **embedded resources** (`<EmbeddedResource Include="Assets/**" />` in `.csproj`). User-supplied content (custom samples, mod files) lives on disk. The `StorageLocation.Unknown` path makes this transparent — the same `AssetInfo` works for both, with disk taking priority.

The `AssemblyExtensions.cs` extension methods (`GetManifestResourceInfo`, `GetManifestResourceStream`, `GetManifestResourceNames` — all overloaded onto `Assembly[]`) flatten the cross-assembly search so callers don't loop manually.

## Lifecycle and threading

- **Construction** is on the main thread, before `OnLoad`.
- **`Load<T>` calls** can happen on any thread, but anything that touches the GPU (compiling a `Shader`, uploading a `GPUTexture`) must be on the GL thread or use the preload / upload queues.
- **`Update()`** is called once per frame from the GL thread by `Game.OnUpdateFrame`. Anything queued during the frame is drained here.

## Related

- The reflection-based preload mechanism that drives this is set up in [[Entrypoint#OnLoad|Game.OnLoad]].
- GPU resources surfaced by loaders (`Shader`, `GPUTexture`, `GLBuffer`) carry the `BufferState` lifecycle — see [[Renderer/Renderer]].
- The [[Renderer/Queues|DeleteQueue]] is the dual of asset loading — it's how loaded GPU resources are torn down safely.
