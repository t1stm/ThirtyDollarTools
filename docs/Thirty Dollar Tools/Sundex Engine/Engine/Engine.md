# Engine

`Sundex.Engine` is the runtime layer: it owns the OpenGL context, drives the game loop, loads assets, renders, manages scenes, and brokers threading. Everything else in the project (`Sundex.Components`, `Sundex.Markup`, `Sundex.Style.DSL`) is built on top of these primitives.

If you only read one section in this vault, this is the one. The whole stack reaches into `Sundex.Engine` somewhere.

## Pages

- [[Entrypoint|Entrypoint]] — `Game.cs`, `GameGlobals`, `GLInfo`, the game loop, debug callbacks, lifecycle.
- [[Asset Management|Asset Management]] — `AssetProvider`, `IAssetLoader<T>`, `CacheProvider`, `ShaderPool`, embedded vs. file vs. cache storage.
- [[Renderer/Renderer|Renderer]] — VAOs, VBOs, shaders, textures, atlases, the deferred [[Renderer/Queues|delete queue]], cameras.
- [[Scene Management|Scene Management]] — `Scene`, `SceneManager`, transitions, init/render/update/resize/keyboard/mouse pipelines.
- [[Text Rendering/Text Rendering|Text Rendering]] — MSDF font rasterization (`GlyphProvider`), `TextProvider`, `TextBuffer`, `TextSlice`, `Batched.vert`/`.frag`.
- [[Threading|Threading]] — `ThreadRunner`, marshalling exceptions back to the GL thread.

## Bird's-eye picture

```
                  Game (GameWindow)
                  │
   ┌──────────────┼──────────────────┬────────────────┐
   │              │                  │                │
AssetProvider  SceneManager     ThreadRunner       GLInfo
   │              │                  │
   │              └──► Scene[]   ┌── exception queue
   │                  │          └── action wrapper for tasks/threads
   │                  └──► UIElement tree (Sundex.Components)
   │
   ├── ShaderPool         (lazy compile + reload)
   ├── CacheProvider      (decoded asset cache to disk)
   └── DeleteQueue        (deferred GPU resource deletion)
```

Every `Scene` is created with a reference to `Game`, so it can reach `AssetProvider`, `SceneManager`, `ThreadRunner`, and `Logger` through it without being wired manually.

## Core abstractions ([Sundex.Core])

A handful of types live in `Sundex.Core` and are consumed by the engine:

- `Renderable` — the abstract base for anything drawable. Has `Position`, `Scale`, `Color`, an `Animations` list, and a `Render(camera)` method.
- `ISeekableStopwatch` / `SeekableStopwatch` — pause-able, seek-able timer used by animations.
- `Animations/` — keyframe animation runtime. See [[../Style DSL/Animations|Animations]] for how the style DSL feeds into this.

## NuGet / package surface

From `Sundex.Engine.csproj`:

- `OpenTK` (5.0-pre.15) — windowing, input, GL bindings.
- `OpenTK.Mathematics` — `Vector2/3/4`, `Matrix4`.
- `Serilog`, `Serilog.Sinks.Console`, `Serilog.Sinks.File`.
- `Msdfgen` (MSDF-Sharp) — vector glyph → MSDF bitmap.
- `SixLabors.ImageSharp` — pixel container, image loading.
- `Microsoft.Extensions.Logging` — abstraction layer used by some sub-systems.

## Common conventions

- **`[PreloadGraphicsContext]`** — applied to `static class`-shaped types that need to upload GPU resources at startup. Discovered by reflection in `Game.OnLoad`. The class must implement `IGamePreloadable` and provide `static void Preload(AssetProvider)`.
- **`BufferState`** — almost every GPU resource (`Shader`, `GLBuffer`, `GPUTexture`) carries a `BufferState` flag (`PendingCreation | Created | PendingUpload | Failed`). State transitions happen lazily on first `Bind()` / `Use()`.
- **Locking** — anywhere that touches a shared mutable collection (`AssetProvider.Update`, `DeleteQueue.Enqueue`, `SceneManager.ActiveScenes`) uses `lock (collection)`. There is no `ConcurrentX`; locks are kept short and explicit.
- **`ReadOnlySpan<char>` lookups** — many dictionaries use `GetAlternateLookup<ReadOnlySpan<char>>()` so callers can query without allocating a `string`.
</content>
</invoke>