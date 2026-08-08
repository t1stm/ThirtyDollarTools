# Queues

Sundex uses small bounded queues (rather than `Concurrent*` collections) to defer work off the GL thread. The most important is the **`DeleteQueue`**, but the same pattern shows up in `GLBuffer.Updates`, `GPUTexture.UploadQueue`, `VertexArrayObject._uploadQueue`, `ShaderPool._shadersToPreload`, and `Game._enqueuedEvents`.

> Source: `Sundex/Sundex.Engine/Renderer/Queues/DeleteQueue.cs`. The pattern is reused across the engine.

## `DeleteQueue`

```csharp
public class DeleteQueue
{
    private readonly Queue<(DeleteType type, int handle)> _queue = new();

    public void Enqueue(DeleteType type, int handle);
    public void ExecuteDeletes();
}

public enum DeleteType { VBO, IBO, VAO, Texture, Shader }
```

A single instance lives on `AssetProvider.DeleteQueue`. Anything that owns a GL handle and is `IDisposable` enqueues here in its `Dispose()` instead of calling `GL.Delete*` directly.

### Why deferred?

Three reasons:

1. **GL thread affinity.** `GL.Delete*` is only valid on the thread that owns the context. `Dispose()` can be called from anywhere — finalisers, GC threads, off-thread cleanup logic.
2. **Frame-coherence.** If you delete a texture mid-render-frame while it is bound to an active sampler, behaviour is undefined. Draining the queue *between* frames (in `AssetProvider.Update`) keeps deletion outside any active draw.
3. **Batching.** Many components free their resources at the same time (scene transitions, layout reflow). One drained queue per frame is faster than many syscalls scattered across the frame.

### Execution

```csharp
public void ExecuteDeletes()
{
    lock (_queue)
    {
        while (_queue.TryDequeue(out var tuple))
        {
            var (type, handle) = tuple;
            if (handle == 0) continue;
            switch (type)
            {
                case DeleteType.VBO:
                case DeleteType.IBO:    GL.DeleteBuffer(handle);      break;
                case DeleteType.VAO:    GL.DeleteVertexArray(handle); break;
                case DeleteType.Texture:GL.DeleteTexture(handle);     break;
                case DeleteType.Shader: GL.DeleteShader(handle);      break;
                default: throw new ArgumentOutOfRangeException();
            }
            RenderMarker.Debug($"Deleted {type}: ({handle})");
        }
    }
}
```

Called once per tick from `AssetProvider.Update()`. The `handle == 0` short-circuit lets callers enqueue defensively even if creation failed.

`VBO` and `IBO` both map to `GL.DeleteBuffer` — OpenGL doesn't distinguish between vertex and index buffers at deletion time, but the enum keeps the call site readable.

A `RenderMarker.Debug` line is emitted for each deletion. In a `RenderDoc` capture this shows up as a labelled marker, making it easy to see when a resource went away.

## The pattern, generalised

Several other queues across the engine follow the same shape:

| Queue | Where | Drained from | Drained when |
|---|---|---|---|
| `DeleteQueue._queue` | `AssetProvider.DeleteQueue` | `AssetProvider.Update()` | Once per frame |
| `GLBuffer.Updates` | per-buffer `Dictionary<int, T>` | `GLBuffer.Update()` (via `VAO.Update`) | Once per frame |
| `GPUTexture.UploadQueue` | per-texture `Queue<Action>` | `GPUTexture.Bind()` if `PendingUpload` | On bind, lazily |
| `VertexArrayObject._uploadQueue` | per-VAO `Queue<(IBuffer, layout)>` | `VAO.Update()` | Once per frame |
| `ShaderPool._shadersToPreload` | per-pool list | `AssetProvider.Update()` | Once per frame |
| `Game._enqueuedEvents` | `Queue<Action<Game>>` | `Game.OnUpdateFrame` | Once per frame |
| `CacheProvider._cachedAssets` | `Queue<(CachedInfo, byte[])>` | `AssetProvider.Update()` | Once per frame |

All of them share the same idiom:

```csharp
private readonly Queue<T> _queue = new();
public void Enqueue(T item) {
    lock (_queue) _queue.Enqueue(item);
}
public void Drain() {
    lock (_queue) while (_queue.TryDequeue(out var item)) Process(item);
}
```

### Why not `ConcurrentQueue<T>`?

Three small wins from the lock-on-`Queue<T>` form:

1. **Atomic drain.** `lock` lets the drain step iterate without other producers interleaving, so the order is "everything queued up to *now*, then unlock." A `ConcurrentQueue` would need explicit batching logic.
2. **Predictable cost.** `Queue<T>.Enqueue/TryDequeue` is `O(1)` with a small constant. Most of these queues hold a handful of items per frame; there's no contention to lose sleep over.
3. **Consistent locking.** The same idiom shows up everywhere; `lock (collection)` is uniform, easy to read, and trivially correct in inspection.

`SemaphoreSlim` is used in one place (`ShaderPool._preloadLock`) where the pool produces *and* consumes from arbitrary threads but only the GL thread drains — basically the same concept.

## Upload queues vs. delete queue

There's an intentional asymmetry between **upload** and **delete**:

- **Upload** queues live *on the resource* (`GPUTexture.UploadQueue`, `GLBuffer.Updates`). They drain on first `Bind` (or on `Update`) — i.e. *just before the resource is used*. This way uploads happen as close as possible to the consumer, with no dead time waiting for a global pump.
- **Delete** is a *single* global queue. Delete events have no consumer to bind against — the resource is going away.

## Threading model summary

```
              non-GL thread                                   GL thread
              ─────────────                                   ─────────
   buffer[i] = value           ┐
   buffer.Dispose()            ├────► lock'd Queue/Dict ─────► Update()  (per frame)
   shader.PreloadShader(...)   │                                 │
   cacheProvider.Enqueue(...)  ┘                                 │
                                                                 ▼
                                                         GL.Delete* / GL.MapBuffer / ...
```

Producers can be anywhere; consumers are always the main render loop (`Game.OnUpdateFrame` → `AssetProvider.Update()` / `Scene.Update()`).

## Related

- [AssetProvider.Update()](../Asset%20Management.md) is the global pump that drains `DeleteQueue`, `ShaderPool`, and `CacheProvider` queues.
- [Buffers](Buffers.md#the-updates-dictionary) uses the same idiom for per-element writes.
- [Textures](Textures.md) uses an upload queue per `GPUTexture`.
