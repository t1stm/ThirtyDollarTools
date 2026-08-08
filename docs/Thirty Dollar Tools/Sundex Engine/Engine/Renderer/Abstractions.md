# Abstractions

The renderer is built around a tight stack of interfaces and a single state-flag enum. Every concrete GPU resource (buffer, shader, texture, VAO) implements at least `IBindable`; everything that owns mutable data on top of that implements `IBuffer`.

> Source: `Sundex/Sundex.Engine/Renderer/Abstract/`, `Sundex/Sundex.Engine/Renderer/Enums/`.

## `BufferState`

```csharp
[Flags]
public enum BufferState
{
    PendingCreation = 1,
    Created         = 1 << 1,
    Failed          = 1 << 2,
    PendingUpload   = 1 << 3
}
```

Every resource starts at `PendingCreation`. The first `Bind()` / `Use()` call:

1. allocates the GL handle (`GL.GenBuffer`, `GL.CreateProgram`, etc.),
2. flips the state to `Created` (or `Failed` if the handle came back as 0),
3. potentially or-s in `PendingUpload` if there's queued data ready to push.

Because it is a `[Flags]` enum and `Created` / `PendingUpload` aren't mutually exclusive, multi-flag checks (`HasFlag`) compose naturally: a created texture with pending pixel data has `Created | PendingUpload`.

## `IBindable`

```csharp
public interface IBindable
{
    BufferState BufferState { get; }
    int Handle { get; }
    void Bind();
}
```

The minimal contract: *I have a GL handle and I can be made current.* `Shader` implements this with `Use()` instead of `Bind()` (OpenGL's terminology for programs vs. buffers), but the shape is the same.

## `IBuffer : IBindable`

```csharp
public interface IBuffer : IBindable
{
    void Update();
}
```

Adds an explicit "push pending changes to GPU" step. Used by `GLBuffer<T>` so a `VertexArrayObject` can call `Update()` on every attached buffer once per frame without caring whether anything actually changed (cheap no-op when there are no queued updates).

## `IGPUBuffer<TDataType> : IBuffer, IDisposable`

The strongly-typed buffer:

```csharp
public interface IGPUBuffer<TDataType> : IBuffer, IDisposable where TDataType : unmanaged
{
    int Capacity { get; }
    TDataType this[int index] { get; set; }
    void Dangerous_SetBufferData(ReadOnlySpan<TDataType> newData);
}
```

`unmanaged` is required for `sizeof(TDataType)` and `fixed (TDataType*)`. `Dangerous_*` is the project's convention for "must be on the GL thread" methods — the prefix makes it grep-able and signals at the call site that wrong-threading is the caller's problem.

## `IRenderable`

```csharp
public interface IRenderable { void Render(Camera camera); }
```

Anything that draws. Implemented by every leaf-renderable (`Label`, `Panel`, `ProgressBar`, etc.) and by the `Renderable` base class in `Sundex.Core`.

## `IPositionable` and `IRectangle`

```csharp
public interface IPositionable
{
    Vector3 Position { get; set; }
    Vector3 Scale { get; set; }
}

public interface IRectangle : IPositionable
{
    Vector4 Rectangle { get; set; }
    // default-impl: Position/Scale wrap Rectangle.XY / Rectangle.ZW
}
```

`IRectangle` packs position + size into a single `Vector4 (X, Y, W, H)` while still presenting an `IPositionable` view. The default-interface implementation means most components only need to expose `Rectangle` and get `Position` / `Scale` for free.

## `IGPUReflection`

```csharp
public interface IGPUReflection
{
    static abstract void SelfReflectToGL(VertexBufferLayout layout);
}
```

Lets a struct *describe its own vertex layout*. Instead of having to manually `layout.PushFloat(2).PushFloat(4)` at every call site, types like `TextCharacter` implement `static SelfReflectToGL` and callers do:

```csharp
var layout = new VertexBufferLayout();
TextCharacter.SelfReflectToGL(layout);
```

This is a clean use of C# 11 static abstract members — no reflection, no generated code, just a contract.

## `IGamePreloadable` + `[PreloadGraphicsContext]`

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class PreloadGraphicsContextAttribute : Attribute { }

public interface IGamePreloadable
{
    static abstract void Preload(AssetProvider assetProvider);
}
```

A class marked `[PreloadGraphicsContext]` and implementing `IGamePreloadable` is automatically discovered and run during [Game.OnLoad](../Entrypoint.md#onload) (via reflection over registered asset assemblies). It is how `GLQuad`, `VertexArrayObject`, [TextProvider](../Text%20Rendering/Text%20Rendering.md), `Label`, etc. get their static-shared GPU resources uploaded before any scene needs them.

The pattern looks like:

```csharp
[PreloadGraphicsContext]
public class GLQuad : IGamePreloadable
{
    public static GLBuffer<float> VBOWithoutUV { get; set; } = null!;
    public static GLBuffer<float> VBOWithUV    { get; set; } = null!;
    public static GLBuffer<int>   EBO          { get; set; } = null!;

    public static void Preload(AssetProvider assetProvider) {
        // ...allocate, fill, upload...
    }
}
```

`null!` is intentional: the field is uninitialised until `Preload` runs, and the contract is "don't touch this before OnLoad."

## `IIndexable<TKey, TValue>` and `IIndexableCollection`

Two small helper interfaces:

```csharp
public interface IIndexable<in TKey, TValue>
{
    TValue this[TKey key] { get; set; }
}

public interface IIndexableCollection<TKey, TValue> : IIndexable<TKey, TValue>
{
    int Count { get; }
}
```

Used by `GLBuffer<T>` so callers can treat it like an array (`buffer[i] = value`) regardless of whether the backing storage is a CPU mirror or a GPU map-buffer call.

## Related

- The lifecycle managed by these flags is what [GLBuffer](Buffers.md), [Shader](Shaders.md), and [GPUTexture](Textures.md) all share.
- The [DeleteQueue](Queues.md) is the dual: every `IBindable.Dispose()` enqueues here.
- The preload mechanism is invoked from [Game.OnLoad](../Entrypoint.md#onload).
