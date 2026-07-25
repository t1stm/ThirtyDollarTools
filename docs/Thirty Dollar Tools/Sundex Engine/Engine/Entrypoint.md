# Entrypoint

The entrypoint of every Sundex application is the **`Game`** class — a subclass of OpenTK's `GameWindow`. It owns the OpenGL context, drives the loop, and instantiates the three top-level managers (asset provider, scene manager, thread runner).

> Source: `Sundex/Sundex.Engine/Game.cs`, `GameGlobals.cs`, `GLInfo.cs`.

## Construction

```csharp
public Game(ILogger serilogLogger, Assembly[] assemblies, GameWindowSettings gameSettings,
    NativeWindowSettings nativeWindowSettings, string id) : base(gameSettings, nativeWindowSettings)
```

A consumer (the visualizer, the dummy debug app, etc.) creates the `Game` once with:

- A **Serilog logger** — `Game` re-contextualises it to `ILogger.ForContext<Game>()` and to a separate `OpenGL` source context for the GL debug callback.
- An **assembly list** — every assembly that should be scanned for embedded assets and `[PreloadGraphicsContext]` types. The executing assembly (`Sundex.Engine`) is always prepended automatically.
- An **id** — used as a tag in the logs to disambiguate when multiple `Game` instances exist (which usually they don't, but the engine doesn't forbid it).

The constructor immediately allocates:

| Field | Type | Purpose |
|---|---|---|
| `AssetProvider` | `AssetProvider` | Loads/queries assets — see [[Asset Management]] |
| `SceneManager` | `SceneManager` | Owns all scenes — see [[Scene Management]] |
| `ThreadRunner` | `ThreadRunner` | Off-thread work + exception marshalling — see [[Threading]] |
| `Globals` | `GameGlobals` | Type-safe key/value bag for app-wide state |
| `GLInfo` | `GLInfo` | Populated during `OnLoad` once the context exists |

## Lifecycle

OpenTK's `GameWindow` calls hooks in this order:

```
ctor → OnLoad → (loop: OnUpdateFrame, OnRenderFrame, OnFramebufferResize, OnFileDrop, ...) → OnClosing
```

### `OnLoad()`

Runs once after the GL context is current. Steps:

1. **Populate `GLInfo`** via `GetGLInfo(GLInfo)` — reads vendor / renderer / version, scans extensions, detects `GL_KHR_debug` and `GL_ARB_direct_state_access`, captures `MaxTextureSize` / `MaxArrayTextureLayers`.
2. **Set GL state** — `BlendFunc(SrcAlpha, OneMinusSrcAlpha)`, enable multisample, enable `DebugOutput` and `DebugOutputSynchronous`.
3. **Wire the GL debug callback** — `_storedDebugCallback = DebugCallback;` (kept as a field to prevent GC of the delegate, per [the OpenTK appendix](https://opentk.net/learn/appendix_opengl/debug_callback.html)). If `GL_KHR_debug` is unsupported, [[Renderer/Renderer#RenderMarker|RenderMarker]] is disabled wholesale.
4. **`ReflectionPreloadObjects(assembly)` for every asset assembly** — finds every type marked `[PreloadGraphicsContext]` that implements `IGamePreloadable`, looks for `static Preload(AssetProvider)` on it via reflection, and invokes it. This is how `TextProvider`, `Label`, `UIContext`, etc. get their shaders/atlases bound at startup without explicit registration.
5. **Hook `AppDomain.UnhandledException`** to log fatal exceptions with the game id.

### `OnUpdateFrame(args)`

Each tick:

```csharp
MakeCurrent();
CursorState = CursorState.Normal;
AssetProvider.Update();          // shader uploads, delete queue, cache writes
ThreadRunner.Update();            // re-throw exceptions captured on worker threads

// drain Game.Enqueue(...) callbacks
lock (_enqueuedEvents)
{
    while (_enqueuedEvents.TryDequeue(out var action))
    {
        action(this);
        SceneManager.Initialize(initArguments);  // initialise any scene the action just queued
    }
}

SceneManager.Initialize(initArguments);  // catch-all for scenes queued elsewhere

if (KeyboardState.IsAnyKeyDown) SceneManager.Keyboard(KeyboardState);
SceneManager.Mouse(MouseState, KeyboardState);
SceneManager.Update(new UpdateArguments { Delta = args.Time });

if (Ctrl+Q) Close();
```

The `_enqueuedEvents` queue exists so that other threads (e.g. the [[Threading|ThreadRunner]] worker) can ask the GL thread to do something next frame via `Game.Enqueue(action)`.

### `OnRenderFrame(args)`

```csharp
GL.Enable(EnableCap.Blend);
GL.Clear(Color | Depth);
GL.ClearColor(0, 0, 0, 0);

SceneManager.Render(new RenderArguments { Delta = args.Time });

GL.Disable(EnableCap.Blend);
Context.SwapBuffers();
```

### Other hooks

- **`OnFramebufferResize`** — tells `SceneManager.Resize(w, h)` and updates the GL viewport.
- **`OnFileDrop`** — forwards file paths to `SceneManager.FileDropped`.
- **`OnClosing`** — calls `SceneManager.Shutdown()`.

## `GLInfo`

A simple POCO populated once during `OnLoad`:

```csharp
public string Vendor, Renderer, Version;
public int MaxTexture2DSize, MaxTexture2DLayers;
public bool SupportsKHRDebug, SupportsDirectStateAccess;
public readonly HashSet<string> Extensions;
```

Used by:

- The renderer to decide whether to enable `GL_KHR_debug` markers (see [[Renderer/Renderer|RenderMarker]]).
- Anything that needs the texture size limits when sizing atlases.

## `GameGlobals`

A tiny type-safe bag:

```csharp
Globals.Set<string>("user_name", "alice");
var name = Globals.Get<string>("user_name");
```

Internally `Dictionary<string, object?>` with `GetAlternateLookup<ReadOnlySpan<char>>()` so callers can query without allocating. `Set<T>` throws on duplicate keys, `Get<T>` throws on missing/wrong-type.

## Debug callback

The GL debug callback decodes the message pointer using stack-allocated UTF-8 → UTF-16 decoding:

```csharp
var bytes = new ReadOnlySpan<byte>(messagePtr.ToPointer(), length);
Span<char> buf = stackalloc char[bytes.Length];
Encoding.UTF8.GetChars(bytes, buf);
```

Then it logs with structured fields (`SourceText`, `TypeText`, `Id`, `SeverityText`, `CallbackMessage`). `DebugType.DebugTypeOther` and `DebugType.DebugTypeMarker` (id 1) are filtered out — those are noise from `RenderMarker` and the GL implementation itself.

## `TryGetScreenScale`

```csharp
public bool TryGetScreenScale(out float h, out float v)
```

Returns the per-monitor DPI scale **except on Wayland**, where it always returns `(1, 1)` because GLFW's monitor scale on Wayland is unreliable. Used by HiDPI-aware UIs.

## Related

- The asset assemblies registered here are walked by [[Asset Management|AssetProvider]] when loading embedded resources.
- The preload mechanism is consumed by [[Renderer/Renderer|the renderer]] (e.g. `GLQuad` is preloaded so VBO + EBO exist before any text or panel tries to draw) and by [[Text Rendering/Text Rendering|TextProvider]] (`Batched` shader).
</content>
</invoke>