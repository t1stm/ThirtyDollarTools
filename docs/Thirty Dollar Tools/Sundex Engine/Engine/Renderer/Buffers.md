# Buffers

The buffer layer covers everything between "raw GL handle" and "ready-to-draw vertex stream": `GLBuffer<T>` (and its CPU-cached variant), `GLQuad` (the universal unit-quad VBO), `VertexArrayObject`, and the `VertexBufferLayout` builder.

> Source: `Sundex/Sundex.Engine/Renderer/Buffers/`, `Sundex/Sundex.Engine/Renderer/VertexArrayObject.cs`, `VertexBufferLayout.cs`, `VertexBufferElement.cs`.

## `GLBuffer<TDataType>`

```csharp
public class GLBuffer<TDataType>(DeleteQueue deleteQueue, BufferTarget bufferTarget)
    : IGPUBuffer<TDataType>, IIndexableCollection<int, TDataType>
    where TDataType : unmanaged
```

A typed wrapper around a single GL buffer object. It can hold **any** unmanaged type — `float`, `int`, a custom vertex struct, a `TextCharacter`, doesn't matter.

### Lazy creation

```csharp
public void Bind()
{
    if (BufferState.HasFlag(BufferState.PendingCreation)) Create();
    if (BufferState.HasFlag(BufferState.Failed))         throw ...;
    GL.BindBuffer(bufferTarget, Handle);
}
```

A buffer doesn't allocate a GL handle until first `Bind()`. This lets you construct buffers on any thread; only binding requires GL.

### The Updates dictionary

`GLBuffer` does not push every write through `glBufferSubData`. Instead it batches:

```csharp
protected Dictionary<int, TDataType> Updates { get; } = new(MaxPreallocatedUpdateCapacity); // 32
```

Setting `buffer[i] = value` writes into `Updates[i] = value`. The actual GPU write happens during `Update()` (called from `VertexArrayObject.Update()` once per frame):

```csharp
var voidPtr = GL.MapBuffer(bufferTarget, BufferAccess.WriteOnly);
var ptr     = (TDataType*)voidPtr;
Span<TDataType> data = new(ptr, Capacity);

foreach (var (index, value) in Updates) data[index] = value;

GL.UnmapBuffer(bufferTarget);
Updates.Clear();
```

Reading a freshly written value (before the upload) returns it from `Updates` directly; reading anything else maps the buffer for read.

### Auto-resize

`SetMemory` watches for index >= Capacity and schedules a resize (1.5× current index) which runs at the start of the next `Update`:

```csharp
if (Capacity < index)
    _newSize = (int)(index * resizeMultiplier);
```

The resize calls `GL.BufferData(target, newSize * sizeof(T), null, StreamDraw)` — i.e. it orphans the old buffer and lets the driver allocate a new backing store. Pending updates from the same frame still apply afterwards.

### `Dangerous_SetBufferData`

```csharp
public virtual unsafe void Dangerous_SetBufferData(ReadOnlySpan<TDataType> newData)
```

Bulk-replaces the entire contents in one `glBufferData` call. Must be called on the GL thread. Used during `Preload` (e.g. `GLQuad`), and any time you have a complete known-size dataset.

### `WithCPUCache`

The nested subclass mirrors GPU contents in a CPU array:

```csharp
public class WithCPUCache(DeleteQueue, BufferTarget) : GLBuffer<TDataType>(deleteQueue, target)
{
    protected TDataType[] CPUBuffer { get; set; } = [];
    public ReadOnlySpan<TDataType> Data => CPUBuffer;
}
```

Reads come straight off the CPU array (no `glMapBuffer` round-trip, no synchronisation point); writes go *both* into the array *and* into `base.Updates`. Used by [[../Text Rendering/Text Rendering|TextBuffer]] so the engine can introspect/iterate the live character list every frame without bouncing off the GPU.

`ResizeCPUBuffer(capacity)` queues a resize and copies the old contents into the new array, then bulk-uploads via `EnqueueNewData` (which is itself a loop of `this[i] = value` calls — i.e. it goes through `Updates`).

### Disposal

`Dispose()` enqueues `DeleteType.VBO` on the `DeleteQueue` and suppresses finalisation. **Don't call `Dispose` on the GL thread directly during a frame** — the queue exists precisely so disposal is decoupled.

## `GLQuad`

```csharp
[PreloadGraphicsContext]
public class GLQuad : IGamePreloadable
{
    public static GLBuffer<float> VBOWithoutUV { get; set; }
    public static GLBuffer<float> VBOWithUV    { get; set; }
    public static GLBuffer<int>   EBO          { get; set; }

    public static void DrawInstanced(int count) =>
        GL.DrawElementsInstanced(PrimitiveType.Triangles, EBO.Capacity,
                                 DrawElementsType.UnsignedInt, IntPtr.Zero, count);
}
```

A single static unit quad shared by every component that needs to instance-draw rectangles (text glyphs, panels, progress bars, etc.). Two VBOs — one with UVs, one without — and a 6-index EBO for two triangles.

Vertices are CCW from top-left:

```
(0,1) ── (1,1)
  │   ╲    │
  │    ╲   │
  │     ╲  │
(0,0) ── (1,0)

indices: 0 1 3, 1 2 3
```

The world-space placement of each instance is supplied via per-instance attributes on a separate VBO (typically `instance_offset`, `instance_scale`, `instance_color`).

## `VertexArrayObject`

```csharp
[PreloadGraphicsContext]
public class VertexArrayObject : IBindable, IGamePreloadable, IDisposable
```

A VAO that lazily collects buffer + layout pairs and uploads them on its first `Update()`:

```csharp
var vao = new VertexArrayObject();
vao.AddBuffer(GLQuad.VBOWithUV,  positionLayout);   // 3 floats pos + 2 floats uv
vao.AddBuffer(instancePerCharVBO, instanceLayout);  // per-instance attributes
vao.SetIndexBuffer(GLQuad.EBO);
// ...later, on draw...
vao.Bind();
vao.Update();   // drains the upload queue, then calls Update() on every attached buffer
```

Internally `_uploadQueue` holds `(IBuffer, VertexBufferLayout)` tuples. On `UploadBuffer`:

1. Bind self, bind buffer, upload buffer.
2. For each element in the layout: `GL.VertexAttribPointer(vi, count, type, normalized, stride, offset)` and `GL.EnableVertexAttribArray(vi)`.
3. If the element has `Divisor != 0` (per-instance), `GL.VertexAttribDivisor(vi, divisor)`.
4. Track `_vertexIndex` so subsequent buffers slot in at the next attribute index.

DSA path: when `GLInfo.SupportsDirectStateAccess` is true, the index buffer is bound via `GL.VertexArrayElementBuffer(Handle, ibo.Handle)` once, instead of being re-bound at every `UploadBuffer` call.

## `VertexBufferLayout` / `VertexBufferElement`

A small fluent builder:

```csharp
var layout = new VertexBufferLayout()
    .PushFloat(3)                  // position
    .PushFloat(2)                  // uv
    .PushFloat(4, perInstance: true)  // colour, per-instance
    .PushMatrix4(1);               // 4 instance attributes for a 4×4 matrix
```

Each `Push*` records type + count + `Divisor` (0 for per-vertex, 1 for per-instance) and updates the running stride. `PushMatrix4(n)` is sugar for `PushMatrix(4, 4, n)`, which expands the matrix into `y` rows of `x` floats — necessary because OpenGL exposes matrices as N consecutive `vec4` attributes.

`VertexBufferElement` is a POD with `Type`, `Count`, `Normalized`, `Divisor`. `VertexBufferExtensions.GetSize(VertexAttribPointerType)` (in `VertexBufferExtensions.cs`) maps GL types to byte sizes for stride computation.

## `GLBufferList` and `TrackedBufferReference`

`GLBufferList<T>` is a lightweight wrapper that manages a logical *list* on top of a `GLBuffer`, supporting append/remove with free-range tracking. `TrackedBufferReference<T>` is a stable handle to a slice within such a list — used by [[../Text Rendering/Text Rendering|TextSlice]] so callers keep a reference to a logical region of a shared character buffer even as other text is added or removed elsewhere.

## Related

- [[Abstractions|`BufferState`]] is the lifecycle these buffers all share.
- The [[Queues|DeleteQueue]] receives `Dispose()` calls.
- [[../Text Rendering/Text Rendering|TextBuffer]] is the largest consumer — it uses `GLBuffer<TextCharacter>.WithCPUCache` plus a `VertexArrayObject` that mixes `GLQuad.VBOWithUV` (per-vertex) with the text-character VBO (per-instance).
