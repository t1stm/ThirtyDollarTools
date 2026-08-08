# Scene Management

A **scene** is the top-level container for application state — analogous to a screen, level, or page. Sundex is intentionally minimal here: no lifecycle FSM, no transition animations, no pause/resume. A scene is just a class with a fixed set of overrides that the engine calls at the right time.

> Source: `Sundex/Sundex.Engine/Scenes/`.

## `Scene` (abstract)

```csharp
public abstract class Scene(Game game)
{
    public Game            Game           { get; } = game;
    public AssetProvider   AssetProvider  => Game.AssetProvider;
    public SceneManager    SceneManager   => Game.SceneManager;
    public ThreadRunner    ThreadRunner   => Game.ThreadRunner;
    public ILogger         Logger         => Game.Logger;

    public abstract void Initialize(InitArguments initArguments);
    public abstract void Start();
    public abstract void Render(RenderArguments renderArgs);
    public abstract void TransitionedTo();
    public abstract void Update(UpdateArguments updateArgs);
    public abstract void Resize(int w, int h);
    public abstract void Shutdown();
    public abstract void FileDrop(string[] locations);
    public abstract void Keyboard(KeyboardState state);
    public abstract void Mouse(MouseState mouseState, KeyboardState keyboardState);

    // virtual, not abstract — default no-op, so scenes that don't need them can ignore them
    public virtual void TextInput(TextInputEventArgs e) { }
    public virtual void KeyDown(KeyboardKeyEventArgs e) { }
}
```

The constructor takes `Game` and the convenience properties (`AssetProvider`, `SceneManager`, `ThreadRunner`, `Logger`) flow from there — no DI container, no service locator, just direct references.

### Lifecycle hooks

| Method | Called by | When |
|---|---|---|
| `Initialize(InitArguments)` | `SceneManager.Initialize()` | First time after `LoadScene` |
| `Start()` | (your code) | After all scenes you want loaded are loaded |
| `TransitionedTo()` | `SceneManager.TransitionTo(...)` | When this scene becomes active |
| `Update(UpdateArguments)` | `Game.OnUpdateFrame` → `SceneManager.Update` | Every tick if active |
| `Render(RenderArguments)` | `Game.OnRenderFrame` → `SceneManager.Render` | Every frame if active |
| `Resize(w, h)` | `Game.OnFramebufferResize` | Window resize |
| `Keyboard(state)` | `Game.OnUpdateFrame` (only if any key down) | Per tick |
| `Mouse(mouseState, keyState)` | `Game.OnUpdateFrame` | Every tick |
| `TextInput(TextInputEventArgs)` | `Game.OnTextInput` → `SceneManager.TextInput` | Unicode text input (virtual, default no-op) |
| `KeyDown(KeyboardKeyEventArgs)` | `Game.OnKeyDown` → `SceneManager.KeyDown` | Key press, including OS repeats (virtual, default no-op) |
| `FileDrop(string[])` | `Game.OnFileDrop` | When OS drops files onto window |
| `Shutdown()` | `Game.OnClosing` | App close |

`Start` is the only hook the engine doesn't call automatically. It exists as a convention for "boot all scenes, then do post-init wiring" — typically the host calls it explicitly after `LoadScene`.

### Argument types

All argument types are `ref struct` — they live on the stack and never escape:

```csharp
public ref struct InitArguments    { public Vector2i StartingResolution; public GLInfo GLInfo; }
public ref struct RenderArguments  { public double Delta; }
public ref struct UpdateArguments  { public double Delta; }
```

`ref struct` was chosen so callers cannot accidentally box or capture these — the engine reuses the same instance and overwrites fields per frame.

## `SceneManager`

```csharp
public class SceneManager(ILogger logger)
{
    public Dictionary<string, Scene> Scenes      { get; } = new();
    public List<Scene>               ActiveScenes { get; } = [];
    private Queue<Scene>             ScenesToInitialize { get; } = [];

    public T LoadScene<T>(ReadOnlySpan<char> sceneName, Func<SceneManager, T> factory) where T : Scene;
    public void TransitionTo(ReadOnlySpan<char> sceneName);
    public void TransitionTo(Scene scene);
    public void TransitionTo(ReadOnlySpan<Scene> scenes);
    public T Get<T>() where T : Scene;
}
```

There are two collections:

- **`Scenes`** — the registry of every loaded scene, keyed by its name. Lookups go through `GetAlternateLookup<ReadOnlySpan<char>>()` so callers don't need to allocate strings.
- **`ActiveScenes`** — the *currently rendered* set, in render order.

A scene is *loaded* exactly once via `LoadScene("scene_name", mgr => new MyScene(game))`. The factory pattern lets the scene receive the `SceneManager` for cross-wiring before it has been added to the registry.

### Initialisation queue

```csharp
private Queue<Scene> ScenesToInitialize { get; } = [];
```

`LoadScene` enqueues onto this queue and returns immediately. The actual `Initialize(initArguments)` call happens later, on the GL thread, when [`SceneManager.Initialize`](Entrypoint.md#onupdateframeargs) drains the queue. This separation matters because `Initialize` typically calls `AssetProvider.Load<...>` to allocate GPU resources — which must be on the GL thread.

The drain happens twice per `OnUpdateFrame`:

```csharp
// Game.OnUpdateFrame
while (_enqueuedEvents.TryDequeue(out var action)) {
    action(this);                      // user code may LoadScene during this
    SceneManager.Initialize(initArguments);  // catch any newly queued scenes
}
SceneManager.Initialize(initArguments); // catch-all
```

This way a scene loaded during a `Game.Enqueue(...)` callback is already initialised before the same frame's `Update`/`Render` kicks in.

### `TransitionTo`

Three overloads:

```csharp
TransitionTo(ReadOnlySpan<char> sceneName);   // by name
TransitionTo(Scene scene);                    // direct
TransitionTo(ReadOnlySpan<Scene> scenes);     // multi-scene composite
```

The "list of scenes" form is the foundation: `ActiveScenes.Clear()`, then `AddRange`, then call `TransitionedTo()` on each. There are no animations or fade overlays — those would be implemented inside the scene if needed.

The list-form lets you stack scenes (e.g. a base game scene + an overlay HUD scene + a debug scene), each fully owning its render loop.

### Locking

Both `Scenes` and `ActiveScenes` use `lock(...)` around mutation and iteration. This makes `LoadScene` / `TransitionTo` callable from any thread, even though the actual lifecycle methods always run on the GL thread.

`ActiveScenes` is iterated via `CollectionsMarshal.AsSpan` in the render path so we don't pay the enumerator allocation per frame.

## Scene composition patterns

The patterns that show up in the Visualizer:

- **Single scene per "screen"** — login screen, main menu, gameplay, settings. Transitions are explicit `TransitionTo("name")` calls.
- **Stacked scenes** — base scene + modal overlay. The overlay scene only handles input when active and the base continues to render below.
- **Persistent scenes** — a debug-stats scene that's added to `ActiveScenes` once and never removed; it just renders its overlay every frame regardless of which "real" scene is also active.

There is no built-in "active vs paused" distinction — if a scene is in `ActiveScenes`, all its hooks fire. If you want a paused scene that still renders, leave it in `ActiveScenes` and gate its `Update` body on a flag.

## Threading

- **Mutation** of scene registries can happen anywhere (locked).
- **All lifecycle hooks** run on the GL thread.
- **Off-thread work** should be marshalled through [`ThreadRunner`](Threading.md) (e.g. a background loader spawning Roslyn compilation), and any GPU-touching follow-up should round-trip through `Game.Enqueue(...)` to land back on the GL thread.

## Related

- [Game](Entrypoint.md) is what calls into `SceneManager` for lifecycle events.
- [ThreadRunner](Threading.md) is exposed on every `Scene` for off-thread work.
- [UIElement](../Components/Components.md) tree is what most scenes contain — see [Components](../Components/Components.md) for how a UI tree gets rendered inside a `Scene.Render` body.
