# Shaders

`Shader` is a thin OpenGL program wrapper: it owns the linked program handle, holds the original `ShaderSource[]` for hot-reload, and exposes a typed `SetUniform(name, value)` API. There is no shader graph, no preprocessor, no `#include` resolver — shaders are vanilla GLSL files loaded through [[../Asset Management|AssetProvider]].

> Source: `Sundex/Sundex.Engine/Renderer/Shaders/Shader.cs`, `Sundex/Sundex.Engine/Asset Management/Types/Shader/`.

## Composition: `ShaderSource` and `ShaderInfo`

A shader program is built from one or more `ShaderSource` objects:

```csharp
public class ShaderInfo : ILoaderInfo
{
    public AssetInfo AssetInfo { get; set; } = new();
    public ShaderType Type { get; set; }   // VertexShader, FragmentShader, ...

    public static ShaderInfo CreateFromUnknownStorage(ShaderType type, string location);
}

public class ShaderSource : ILoadableAsset<ShaderSource, ShaderInfo>
{
    public ShaderInfo Info       { get; set; }
    public ShaderType Type       { get; set; }
    public string     SourceCode { get; set; } = string.Empty;
    public static IAssetLoader<ShaderSource, ShaderInfo> AssetLoader { get; } = new ShaderLoader();
}
```

Each `ShaderSource` is loaded via the standard asset pipeline — `assetProvider.LoadShaders(...)` returns an array of `ShaderSource` with their text already pulled from disk or assembly.

## `Shader`

```csharp
public class Shader(AssetProvider assetProvider, params ShaderSource[] shaderSource) : IDisposable
{
    public ShaderSource[] Sources    { get; }
    public BufferState    BufferState { get; set; } = BufferState.PendingCreation;
    public bool           IsPedantic { get; set; } = false;
    protected int         Handle     { get; set; }

    public void Use();
    public void ReloadShader();
    public bool SetUniform(string name, ...);
    public void Dispose();

    public static Shader Dummy { get; } = new(null!) { Handle = 0 };
}
```

### Compilation

`Use()` is the entry point and triggers `Load()` on first invocation:

1. If `BufferState == Created`, delete the existing program (used during `ReloadShader`).
2. `GL.CreateProgram()` → new handle.
3. **Rent an `int[]`** from `ArrayPool<int>.Shared` for the per-stage shader handles (no GC pressure on hot reloads).
4. For each `ShaderSource`: `GL.CreateShader(type)` → `GL.ShaderSource` → `GL.CompileShader` → `GL.AttachShader(program, handle)`.
5. `LinkAndThrowOnError()` — calls `GL.LinkProgram`, checks `LinkStatus`, throws with the program info log on failure.
6. **Detach + delete** every per-stage shader (the program keeps everything it needs after linking).
7. Flip state to `Created` (or `Failed` on exception) and return the rented array.

This puts the program in a clean state — no orphaned shader objects hanging off it.

### `Dummy` shader

`Shader.Dummy` is a singleton with `Handle = 0`. It's used as a sentinel by code paths that expect a `Shader` reference but want to express "no shader yet" without nullability gymnastics. A real `Use()` on the dummy would fail the `Handle >= 1` check.

### `ReloadShader`

```csharp
public void ReloadShader()
{
    var assetInfoHolder = ArrayPool<ShaderInfo>.Shared.Rent(Sources.Length);
    var shaderInfos = assetInfoHolder.AsSpan();
    try {
        for (var i = 0; i < Sources.Length; i++) shaderInfos[i] = Sources[i].Info;
        assetProvider.Load(Sources, shaderInfos);  // re-pulls source text
        Load();                                    // re-compiles
    }
    finally { ArrayPool<ShaderInfo>.Shared.Return(assetInfoHolder); }
}
```

Re-reads the source text from disk/assembly and recompiles in place. The handle changes; existing uniform-location lookups that were cached externally would need to be re-fetched, but `Shader.SetUniform` always re-queries via `GL.GetUniformLocation` per call so callers don't need to care.

The `AssetProvider.Load` overload taking `Span<TReturn>` + `ReadOnlySpan<TCreate>` is what makes `assetProvider.Load(Sources, shaderInfos)` work — it walks both spans pairwise.

`ShaderPool.Reload()` calls this on every cached shader — handy for live development.

## Uniform binding

```csharp
public bool SetUniform(string name, int value);
public bool SetUniform(string name, float value);
public bool SetUniform(string name, Vector2 value);
public bool SetUniform(string name, Vector3 value);
public bool SetUniform(string name, Vector4 value);
public bool SetUniform(string name, Matrix4 value);
```

Each variant:

1. Calls `GL.GetUniformLocation(Handle, name)`.
2. If `-1` (uniform doesn't exist or was optimised out): returns `false` (default) or throws (`IsPedantic`).
3. Otherwise calls the appropriate `GL.Uniform*f` / `GL.UniformMatrix4f`.

The boolean return lets callers detect inactive uniforms cheaply. `IsPedantic = true` is the development-time strict mode — useful when authoring shaders to catch typos in uniform names.

There is no uniform location cache. Each `SetUniform` re-queries the location. For 2D UI that's negligible; for a high-poly 3D pipeline you'd want something smarter, but Sundex is 2D-only.

## Conventions used in shaders

By inspection of the `.vert`/`.frag` files referenced from the engine:

- **Vertex shaders** receive `vp_matrix` (uniform `Matrix4`) and per-instance attributes for offset, scale, colour.
- **Fragment shaders** typically take a single 2D texture sampler and an alpha multiplier.
- **The MSDF text shader pair** (`Assets/Shaders/Text/Batched.vert/.frag`) reads UV from `GLQuad.VBOWithUV`, instance data from a `GLBuffer<TextCharacter>`, and a 2048×2048 RGBA32F atlas — see [[../Text Rendering/Text Rendering]].

## Lifetime and threading

- **Construction** is cheap and thread-safe — it just stores the `ShaderSource[]` reference.
- **`Use()` / `Load()` must be called on the GL thread.**
- **`Dispose()`** calls `GL.DeleteProgram` directly. Note the asymmetry with `GLBuffer<T>` and `GPUTexture` (which use the [[Queues|DeleteQueue]]) — programs are owned by the [[../Asset Management#Shaders and the ShaderPool|ShaderPool]] which is itself drained on the GL thread, so direct deletion is safe.

## Related

- [[../Asset Management#Shaders and the ShaderPool|ShaderPool]] caches `Shader` instances by name.
- [[Buffers|GLBuffer]] feeds per-vertex / per-instance data; the shader's `vp_matrix` uniform comes from a [[Textures|Camera]].
- The MSDF shader pair powers [[../Text Rendering/Text Rendering|TextProvider]].
