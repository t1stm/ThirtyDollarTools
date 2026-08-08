# Renderer

The renderer is the part of `Sundex.Engine` that owns GPU resources: VAOs, VBOs/EBOs, shaders, textures, atlases, and the deletion queue. It is intentionally thin — there is no scene graph, no built-in batcher, no material system. Higher layers ([Components](../../Components/Components.md), [TextProvider](../Text%20Rendering/Text%20Rendering.md)) compose these primitives into something usable.

> Source: `Sundex/Sundex.Engine/Renderer/`

## Pages

- [Abstractions](Abstractions.md) — `IBindable`, `IBuffer`, `IGPUBuffer<T>`, `IRenderable`, `IRectangle`, `IGPUReflection`, `IGamePreloadable`, plus the `BufferState` lifecycle flag.
- [Buffers](Buffers.md) — `GLBuffer<T>` and its `WithCPUCache` subclass, `GLBufferList`, `GLQuad`, `VertexArrayObject`, `VertexBufferLayout`.
- [Shaders](Shaders.md) — `Shader`, `ShaderSource`, hot-reload, uniform binding.
- [Textures](Textures.md) — `GPUTexture`, `MipmapMode`, `UploadInfoProvider<TPixel>`, `IAtlas`, `GuillotineAtlas`, `GPUTextureAtlas`.
- [Queues](Queues.md) — `DeleteQueue` + the upload queues that live on individual resources.

## Bird's-eye picture

```
                ┌──────────────── DeleteQueue ─────────────┐
                │  (locked Queue<(DeleteType, int)>)       │
                │  drained on AssetProvider.Update()       │
                └──────────────────────────────────────────┘
                            ▲                  ▲
                            │ Enqueue(...)     │
                            │                  │
   IBindable  ◄─── Shader ◄─┘   GLBuffer<T> ───┘   GPUTexture
   ─────────       ─────────    ────────────       ──────────
   BufferState     compile +    map / sub-update   TexSubImage2D
   Handle          link prog    via Updates dict   via UploadQueue
   Bind()          uniforms     CPU cache variant  mipmap setup

           VertexArrayObject
           ─────────────────
           collects (IBuffer, VertexBufferLayout) tuples
           uploads on first Update()
           supports per-instance divisors

                       ▲
                       │ Render(Camera) — orthographic 2D matrix
                       │
                  IRenderable
```

## Conventions

- **Lazy creation** — every GPU resource starts in `BufferState.PendingCreation`. The first `Bind()` / `Use()` calls `GL.Gen*`, switching it to `Created`. Failures flip it to `Failed` and any subsequent bind throws. See [Abstractions](Abstractions.md#bufferstate).
- **Deferred deletion** — never call `GL.Delete*` directly; enqueue on `AssetProvider.DeleteQueue`. The queue runs on the GL thread once per frame from `AssetProvider.Update()`. See [Queues](Queues.md).
- **Static `Preload(AssetProvider)`** — `[PreloadGraphicsContext]` types provide a static method that the engine invokes on `OnLoad` so shaders and shared VBOs are uploaded before any scene tries to use them. See [Abstractions](Abstractions.md#igamepreloadable-preloadgraphicscontext).
- **No backend abstraction** — direct `GL.*` calls. There is no Vulkan/Metal switch and no plan to add one. The engine targets OpenGL 4.5+ with a graceful fall-back path when DSA / `GL_KHR_debug` are unavailable.

## What is *not* here

- A material system. Shaders carry their own uniform setters, and components call them directly.
- A render graph or pass system. The order of `Render` calls is whatever order [Scene](../Scene%20Management.md) / [UIElement](../../Components/Components.md) decide.
- Render-thread synchronisation primitives beyond simple locks on small queues.

## Related

- The lifecycle of these resources is owned by [AssetProvider](../Asset%20Management.md) (via the `DeleteQueue` and `ShaderPool`).
- `[PreloadGraphicsContext]` is discovered by reflection in [Game.OnLoad](../Entrypoint.md#onload).
- Higher-level rendering — [MSDF text](../Text%20Rendering/Text%20Rendering.md), component layout — is built directly on these abstractions.
