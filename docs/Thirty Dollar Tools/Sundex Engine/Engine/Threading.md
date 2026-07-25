# Threading

The engine has one threading helper — `ThreadRunner` — that exists for a single purpose: **catch exceptions on background threads and re-throw them on the GL thread**, so a worker crash doesn't disappear into the void.

> Source: `Sundex/Sundex.Engine/Threading/ThreadRunner.cs`.

## `ThreadRunner`

```csharp
public class ThreadRunner(ILogger logger)
{
    private readonly Queue<ExceptionDispatchInfo> _exceptions = [];
    public ILogger Logger { get; } = logger;

    public Thread RunThread(Action action);
    public Task   RunTask(Action action);
    public void   AddException(ExceptionDispatchInfo exception);
    public void   Update();
}
```

A single instance lives on `Game.ThreadRunner` and is exposed on every `Scene`.

### `RunThread` and `RunTask`

```csharp
public Thread RunThread(Action action) => RunThread(action, AddException);
public Task   RunTask(Action action)   => Task.Run(() => ActionWrapper(action, AddException));
```

Both wrap the user-supplied `Action` in `ActionWrapper`:

```csharp
private static void ActionWrapper(Action action, Action<ExceptionDispatchInfo> exceptionHandler)
{
    try
    {
        action();
    }
    catch (Exception e)
    {
        exceptionHandler(ExceptionDispatchInfo.Capture(e));
    }
}
```

`ExceptionDispatchInfo.Capture(e)` is the .NET BCL primitive for *preserving* an exception (with its full stack trace) so it can be re-thrown on a different thread without losing fidelity. It's the difference between:

```
Exception: Foo bar
   at OtherThread.Worker()       <-- preserved
   at GLThread.Update()          <-- the re-throw site
   --- End of stack trace from previous location ---
   at GLThread.Update()
```

vs. a manual `throw e;` which would mangle the stack.

### `Update`

```csharp
public void Update()
{
    lock (_exceptions)
    {
        while (_exceptions.TryDequeue(out var exception)) exception.Throw();
    }
}
```

Called once per tick from `Game.OnUpdateFrame` (right after `AssetProvider.Update()`). Drains the queue and re-throws every exception in turn — so any worker exception caught last frame becomes a thrown exception on the GL thread *this* frame, and is then handled by `AppDomain.UnhandledException` which logs and tears the app down with full context.

## When to use

Three things this is **for**:

1. **CPU-bound parallel work** that has no GL dependency — Roslyn script compilation, sample loading from disk, parsing markup. Spawn via `RunTask` and don't await.
2. **Long-running background loops** — background asset preloaders, file watchers. Use `RunThread` (raw `Thread` so it can be daemon-style).
3. **Anything where you'd otherwise write `_ = Task.Run(...)`** — losing exceptions is the common bug `ThreadRunner` exists to prevent.

Three things this is **not** for:

1. **GL calls** — workers cannot touch GL state. Round-trip back to the GL thread via `Game.Enqueue(action)`, which queues onto `Game._enqueuedEvents` and runs on next `OnUpdateFrame`.
2. **Synchronisation primitives** — there's no `await` integration. If you need composition, use vanilla `Task` / `async`.
3. **Tight per-frame scheduling** — there's no thread pool tuning, no priority queue. It's a fire-and-forget tool.

## Round-trip pattern

The canonical pattern when you want background work to produce GPU resources:

```csharp
ThreadRunner.RunTask(() =>
{
    // off-thread: parse, decode, compile, etc.
    var image = ExpensiveDecode(path);

    // marshal back to GL thread
    Game.Enqueue(game =>
    {
        var texture = new GPUTexture { Width = image.Width, Height = image.Height };
        texture.QueueUploadToGPU(image.Frames.RootFrame);
        // ... use texture ...
    });
});
```

`Game.Enqueue` is the dual of `ThreadRunner.RunTask`: take a callback off-thread, run it on the GL thread next frame. Together they form a half-duplex marshal.

## Why no exception is silently logged

A worker exception could be logged, swallowed, and the app continues — but that's a deliberate bad-default. Any uncaught exception on a worker thread in this engine *will* eventually crash the GL thread, by design:

- Crashes are loud. Silent worker failures lead to half-functional UIs and confused bug reports.
- The `Update`-time re-throw goes through `AppDomain.UnhandledException` (hooked in [[Entrypoint#OnLoad|Game.OnLoad]]) which tags the log line with the game id, then the OS terminates the process.
- Crash dumps capture the original off-thread stack via `ExceptionDispatchInfo`.

If you genuinely want to swallow an exception, do it inside the worker `try`/`catch` — at which point you've taken responsibility for logging it.

## Related

- [[Entrypoint|`Game.OnUpdateFrame`]] is what drains the exception queue.
- [[./Asset Management|AssetProvider]]'s `_cachedAssets` queue is a typical consumer — files are decoded off-thread and the byte payload bounces back here for the GL-thread-only `File.WriteAllBytes` call.
- [[../Markup/Phases/Parsing Logic|Roslyn script compilation]] in the markup pipeline is the largest in-tree consumer of `RunTask`.
