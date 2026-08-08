# Parsing Logic

The `<logic>` block is compiled into an `Action<object?>` that the host invokes after the component is built. C# is the only language plugin shipped today; the structure supports more.

> Source: `Sundex/Sundex.Markup/Logic/`.

## The plugin shape

```csharp
public abstract class SundexScript
{
    public abstract Action<object?> Compile(
        string sourceCode,
        ISundexContext context,
        SundexComponent component,
        List<string> logicLanguageImports);
}
```

A language plugin is something that takes raw source code (plus context and the just-built component) and returns a delegate. The delegate is what eventually runs the script when the host calls `component.RunLogic?.Invoke(target)`.

`Compile` runs once at component build time. The returned delegate is reused for every invocation — a script compiled once can run many times against different `target` values.

## `LanguageProvider` — the registry

```csharp
public static class LanguageProvider
{
    public static Dictionary<string, SundexScript> Languages { get; } = new() {
        { "csharp", new CSharp() }
    };
}
```

A static dictionary keyed by the `language="..."` attribute value in `<logic>`. Single-instance plugins — the dictionary holds one `CSharp()` for the entire app lifetime; it's reused across all components.

A future Lua or Python plugin would just add an entry. The dispatcher in [`ComponentBuilderV1`](Component%20Builders.md):

```csharp
LanguageProvider.Languages.TryGetValue(logic.Language, out var language);
if (language is null)
    throw new NotSupportedException($"Language {logic.Language} is not supported.");
```

throws on unknown language strings.

## `CSharp` — the Roslyn backend

```csharp
public class CSharp : SundexScript
{
    public override Action<object?> Compile(string sourceCode, ISundexContext context,
        SundexComponent component, List<string> logicLanguageImports)
    {
        var options = ScriptOptions.Default
            .AddReferences([typeof(CSharp).Assembly, ..context.UIContext.AssetProvider.AssetAssemblies])
            .AddImports("System", "Sundex.Markup.Abstract", "Sundex.Markup.Logic.Languages.CSharp",
                        "Sundex.Components", "Sundex.Components.Abstractions")
            .AddImports(logicLanguageImports);

        var script = CSharpScript.Create(sourceCode, options, typeof(ScriptGlobals));
        script.Compile();

        return obj => {
            var globals = new ScriptGlobals(context, component, obj);
            script.RunAsync(globals).GetAwaiter().GetResult();
        };
    }

    public class ScriptGlobals(ISundexContext sundex, SundexComponent component, object? contextObject)
    {
        [UsedImplicitly] public ISundexContext Sundex    { get; } = sundex;
        [UsedImplicitly] public SundexComponent Component { get; } = component;
        [UsedImplicitly] public object? Context           { get; set; } = contextObject;

        [UsedImplicitly]
        public static T As<T>(object? obj) =>
            obj is T casted ? casted
                            : throw new InvalidCastException($"Cannot cast {obj?.GetType()} to {typeof(T)}");
    }
}
```

Three pieces: script options, the actual compile + return delegate, and `ScriptGlobals`.

### Script options — references and imports

```csharp
var options = ScriptOptions.Default
    .AddReferences([typeof(CSharp).Assembly, ..context.UIContext.AssetProvider.AssetAssemblies])
    .AddImports("System", "Sundex.Markup.Abstract", ...)
    .AddImports(logicLanguageImports);
```

**References** are which assemblies the script can resolve types from:

- `typeof(CSharp).Assembly` — the `Sundex.Markup` assembly itself, so the script can use `SundexComponent`, `ISundexContext`, etc.
- `context.UIContext.AssetProvider.AssetAssemblies` — all asset-loadable assemblies registered with the [`AssetProvider`](../../Engine/Asset%20Management.md). This is what lets a script use host-app types (e.g. `MyApp.Settings` from the application's main assembly).

**Imports** are `using` directives prepended invisibly. The five hard-coded imports cover the typical script's needs (you can write `Component.GetID<Button>(...)` without `using Sundex.Components.Labels;`). `logicLanguageImports` is the list parsed from `<logic imports="...">` — host-app namespaces.

The comment in source is interesting:

```
/* AddReferences doesn't work for some reason on SingleFilePublish when -p:IncludeAllContentForSelfExtract is not enabled.
 * See: https://github.com/dotnet/roslyn/issues/50719 */
```

A known Roslyn issue with single-file publish — Roslyn needs assembly files on disk to resolve references, but single-file publish bundles them. The workaround: enable `IncludeAllContentForSelfExtract` so the runtime extracts assemblies to a temp directory.

### `script.Compile()` — at build time

```csharp
var script = CSharpScript.Create(sourceCode, options, typeof(ScriptGlobals));
script.Compile();
```

Compile happens *now*, during `ComponentBuilderV1.CreateComponent`. This is the slow part — Roslyn's scripting engine generates IL on-demand.

For large scripts this can take 100ms+. For typical UI wiring scripts (a few `OnClick` assignments), it's <50ms. Either way, it happens once per component load — the script delegate is cached for re-runs.

Errors at this step throw immediately from `Compile` — typos, missing types, syntax errors all surface during component build. The error messages are Roslyn's standard diagnostics, so usually quite good.

### The returned delegate — runs at invoke time

```csharp
return obj => {
    var globals = new ScriptGlobals(context, component, obj);
    script.RunAsync(globals).GetAwaiter().GetResult();
};
```

The returned `Action<object?>` builds a fresh `ScriptGlobals` for every invocation. The `obj` parameter — passed by the host as `component.RunLogic?.Invoke(host)` — becomes `globals.Context`.

`script.RunAsync(globals).GetAwaiter().GetResult()` is the awkward sync-over-async pattern. Roslyn's scripting API is async-only (it has to be — scripts can use `await`); `GetAwaiter().GetResult()` blocks the calling thread. The component is expected to be invoked from the GL thread, so blocking is fine — it's the same thread that would otherwise be doing more UI work.

The script body sees `Sundex`, `Component`, `Context`, and `As<T>` as if they were global identifiers, because they're members of `ScriptGlobals`, which Roslyn injects as the script's enclosing scope.

## `ScriptGlobals` — what scripts see

```csharp
public class ScriptGlobals(ISundexContext sundex, SundexComponent component, object? contextObject)
{
    [UsedImplicitly] public ISundexContext Sundex    { get; } = sundex;
    [UsedImplicitly] public SundexComponent Component { get; } = component;
    [UsedImplicitly] public object? Context           { get; set; } = contextObject;

    [UsedImplicitly]
    public static T As<T>(object? obj) =>
        obj is T casted ? casted
                        : throw new InvalidCastException($"Cannot cast {obj?.GetType()} to {typeof(T)}");
}
```

Three properties and one static helper, all marked `[UsedImplicitly]` so ReSharper/Rider don't warn about apparent disuse.

| Member | What it gives the script |
|---|---|
| `Sundex` | The `ISundexContext` — call `Sundex.ResolveComponent("...")` to find imports, `Sundex.RegisterElementFactory(...)` to add tags. |
| `Component` | The just-built `SundexComponent` — call `Component.GetID<Button>("save_btn")` to find UI elements. |
| `Context` | The `obj` argument from `RunLogic.Invoke(obj)`. Typically the host class. Settable so scripts can re-assign mid-run if they want. |
| `As<T>(obj)` | Friendly cast helper — throws `InvalidCastException` with a clear message instead of returning null. |

### `Context` is settable

```csharp
public object? Context { get; set; } = contextObject;
```

The setter lets a script swap context mid-execution. Rare but occasionally useful — e.g. a script that initialises one host then dispatches a sub-action with a different host. Most scripts treat it as read-only.

### `As<T>` — the friendly cast

```csharp
var host = As<SettingsHost>(Context);
host.SaveButton = Component.GetID<Button>("save_btn");
```

vs. the raw equivalent:

```csharp
var host = (SettingsHost)Context;     // throws InvalidCastException with no message
```

`As<T>` includes the source type in the exception message — far easier to debug than raw casts.

It's `static` because Roslyn's scripting host imports the globals class as `using static`-style — `As<T>(...)` works without a `globals.` prefix in script source.

## Typical script body

```xml
<logic language="csharp" imports="MyApp.Settings">
    var host = As&lt;SettingsHost&gt;(Context);
    host.Component = Component;

    host.SaveButton   = Component.GetID&lt;Button&gt;("save_btn");
    host.CancelButton = Component.GetID&lt;Button&gt;("cancel_btn");
    host.TitleLabel   = Component.GetID&lt;Label&gt;("title");

    host.SaveButton.OnClick   = _ => host.OnSave();
    host.CancelButton.OnClick = _ => host.OnCancel();
</logic>
```

Pattern:
1. **Cast `Context`** to the host type via `As<T>`.
2. **Resolve UI elements** by ID into the host's `[SetFromLogic]` fields.
3. **Wire up event handlers** — `OnClick` callbacks delegate to host methods.

This is the canonical "dumb script, smart host" division of responsibility. The script is just glue; logic lives in the host class. That's what the [`[SetFromLogic]` mechanism](../Component%20Definition.md#setfromlogic-declarative-wiring-contract) is built around — it lets the host declare *what* must be wired, and `RunLogicAndVerify` checks the script did its job.

The XML entity escaping (`&lt;` for `<`, `&gt;` for `>`, `&amp;` for `&`) is required because the script lives inside an XML element. It's ugly. Ways to mitigate:

- Move the script to an external `src="logic.cs"` file.
- Wrap in a `<![CDATA[...]]>` block — XML's CDATA section disables entity parsing entirely.

CDATA is the more common choice for non-trivial scripts:

```xml
<logic language="csharp">
    <![CDATA[
    var host = As<SettingsHost>(Context);
    if (host.Count > 0 && host.Items[0] != null)
        host.Items[0].OnClick = _ => Console.WriteLine("clicked");
    ]]>
</logic>
```

`InnerText` strips the CDATA wrapper transparently, so the parser sees the script source exactly as written.

## Threading and exceptions

`script.RunAsync(...).GetAwaiter().GetResult()` blocks the calling thread. If the script throws, the exception propagates out of `GetResult()` to the caller of `RunLogic.Invoke(...)`. Typical wrapping:

```csharp
try {
    component.RunLogic?.Invoke(host);
} catch (CompilationErrorException ex) {
    logger.Error("Script compile error: {Message}", ex.Message);
} catch (Exception ex) {
    logger.Error(ex, "Script runtime error");
}
```

Roslyn raises `Microsoft.CodeAnalysis.Scripting.CompilationErrorException` for compile failures (which would have already happened in `Compile()`, but if you re-Compile externally...) and ordinary exceptions for runtime failures.

If the script does work that should be off-thread, it's the script's responsibility to use `Sundex.UIContext` to find a `ThreadRunner` (via the host) and queue work. The plugin doesn't impose any threading.

## Why C# scripting and not source-generated code?

Source generators run at build time and produce real C# files. They're faster (no Roslyn at runtime), but require a build step — not viable for hot-loaded markup files.

C# scripting is the runtime equivalent: parse and JIT the source on demand. Slow to compile, fast to run, hot-reloadable. The right trade-off for "designer changes a `.snx.xml` file and reloads."

The `Microsoft.CodeAnalysis.CSharp.Scripting` package adds ~10MB to the binary. For Sundex's use case — a content-creation tool where iteration speed matters — that's worth it.

## Adding a new language

Three steps:

1. **Subclass `SundexScript`**:
   ```csharp
   public class Lua : SundexScript {
       public override Action<object?> Compile(string source, ISundexContext ctx,
           SundexComponent component, List<string> imports) { ... }
   }
   ```

2. **Register in `LanguageProvider`**:
   ```csharp
   public static Dictionary<string, SundexScript> Languages { get; } = new() {
       { "csharp", new CSharp() },
       { "lua",    new Lua() }
   };
   ```

3. **Use it from markup**:
   ```xml
   <logic language="lua">...</logic>
   ```

The plugin is responsible for converting the script's globals into whatever idiom the language uses — for Lua, that'd typically be a global table; for Python, module-level names.

## Related

- [Component Builders](Component%20Builders.md) — the orchestrator that calls `Compile`.
- [RunLogic](../Component%20Definition.md#runlogic-the-compiled-script) — the storage point for the compiled delegate.
- [RunLogicAndVerify](../Component%20Definition.md#runlogicandverify-defensive-script-execution) / [`[SetFromLogic]`](../Component%20Definition.md#setfromlogic-declarative-wiring-contract) — the verification pattern this plays into.
- [Threading](../../Engine/Threading.md) — for off-thread script invocation.
