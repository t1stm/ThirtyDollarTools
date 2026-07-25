# Component Definition

A **component** in Sundex is a built tree of `UIElement`s plus the metadata to find named elements inside it and to run its associated logic script. The `SundexContext` is the registry that holds all loaded components and the builders/factories used to make new ones.

> Source: `Sundex/Sundex.Markup/SundexComponent.cs`, `Sundex/Sundex.Markup/SundexContext.cs`, `Sundex/Sundex.Markup/Abstract/`, `Sundex/Sundex.Markup/Attributes/`.

## `ISundexComponent` — the interface

```csharp
public interface ISundexComponent
{
    public string?  Name    { get; init; }
    public ISundexContext Context { get; }
    public UIElement Element { get; set; }
    public HashSet<ISundexComponent> Dependencies { get; init; }
    public List<ISundexComponent>    Children     { get; init; }
}
```

A component is:

- **Named** (optional) — if present, can be `import`ed from another component's markup.
- **Bound to a context** — the `ISundexContext` it was built in.
- **Rooted at a `UIElement`** — the realised UI tree.
- **A dependency graph** — `Dependencies` are components imported by this one; `Children` are components used by this one (currently mostly equivalent — the distinction is reserved for future composition patterns).

## `SundexComponent` — the concrete

```csharp
public class SundexComponent : ISundexComponent
{
    public required string Version { get; set; }
    public Action<object?>? RunLogic { get; set; }

    public Dictionary<string, UIElement>       RegisteredIDs     { get; init; } = [];
    public Dictionary<string, List<UIElement>> RegisteredClasses { get; init; } = [];
    public required ISundexContext Context { get; init; }
    public required UIElement       Element { get; set; }

    public HashSet<ISundexComponent> Dependencies { get; init; } = [];
    public List<ISundexComponent>    Children     { get; init; } = [];
    public string? Name { get; init; }

    public T GetID<T>(string id) where T : UIElement
    {
        if (!RegisteredIDs.TryGetValue(id, out var element))
            throw new Exception($"Unable to find element with id: {id}");
        return element as T ?? throw new Exception($"Element with id: {id} is not of type {typeof(T)}");
    }
}
```

### `RegisteredIDs` and `RegisteredClasses`

The builder walks the `SundexNode` tree once during construction and populates these two dictionaries:

```csharp
if (!string.IsNullOrEmpty(node.Id))
    registeredIds[node.Id] = element;

foreach (var @class in node.Classes ?? []) {
    if (!registeredClasses.TryGetValue(@class, out var list))
        list = registeredClasses[@class] = [];
    list.Add(element);
}
```

That's the full machinery — no scoping, no nested namespaces. IDs are component-global; classes are component-global. Two elements with the same `id` would clobber each other in the dictionary (last write wins), so don't do that.

### `GetID<T>` — typed lookup

```csharp
var saveBtn = component.GetID<Button>("save_btn");
saveBtn.OnClick = _ => Save();
```

This is the primary API for logic scripts to reach into the tree. The double-cast pattern (lookup → as T) gives a clear error message at the failure point: "Unable to find element with id" vs "Element with id is not of type Button".

There's no equivalent `GetClass<T>` method — but `RegisteredClasses["primary"]` returns the list directly, and you can `.Cast<Button>().ToList()` from there.

### `RunLogic` — the compiled script

```csharp
public Action<object?>? RunLogic { get; set; }
```

A nullable delegate. If the document had a `<logic>` section, [[Phases/Parsing Logic|`CSharp.Compile`]] returns a delegate that closes over the compiled Roslyn `Script<object>` and a freshly-constructed `ScriptGlobals`. That delegate is stored here.

Calling `component.RunLogic?.Invoke(this)` is the standard "run the script with `this` as the script context" idiom. The `object?` parameter becomes `ScriptGlobals.Context` inside the script — typically the host class that owns the component.

`Version` is the builder version string ("1.0" today). Stored so that future hot-reload or migration code can route old documents through the right builder.

## `ISundexContext` — the runtime registry

```csharp
public interface ISundexContext
{
    UIContext UIContext { get; }
    ISundexComponent ResolveComponent(ReadOnlySpan<char> dependency);
    void RegisterComponent(ISundexComponent component);
    void RegisterElementFactory(string tagName, Func<UIContext, UIElement> factory);
    UIElement? CreateElement(string tagName);
}
```

Three responsibilities:

1. **Hold the `UIContext`** — every component built from this context shares one [[../Components/Abstractions#UIContext|`UIContext`]], which means one render queue, one TextProvider, one cursor callback.
2. **Component registry** — `RegisterComponent` / `ResolveComponent` for cross-component imports.
3. **Custom tag factory** — `RegisterElementFactory(tagName, factory)` lets the host add new tags. The builder calls `CreateElement(tagName)` for any tag it doesn't recognise built-in (see [[Phases/Parsing Markup|Parsing Markup]]).

## `SundexContext` — the concrete

```csharp
public class SundexContext(UIContext context) : ISundexContext
{
    public Dictionary<string, IComponentBuilder> ComponentBuilderVersions { get; } = new() {
        { ComponentBuilderV1.Version, new ComponentBuilderV1() }
    };
    public Dictionary<string, ISundexComponent>           LoadedComponents  { get; } = [];
    public Dictionary<string, Func<UIContext, UIElement>> ElementFactories  { get; } = [];
    public UIContext UIContext { get; } = context;

    public SundexComponent NewComponent(string smxlMarkup);
    public void            RegisterBuilder(string version, IComponentBuilder builder);
    public void            RunLogicAndVerify(SundexComponent component, params ReadOnlySpan<Func<object?>> objectGetters);
}
```

### `NewComponent(markup)` — the user-facing entry point

```csharp
public SundexComponent NewComponent(string smxlMarkup)
{
    var layout = MarkupParser.Parse(smxlMarkup);
    var version = layout.Root.Version;
    var lookup = ComponentBuilderVersions.GetAlternateLookup<ReadOnlySpan<char>>();
    return !lookup.TryGetValue(version, out var builder)
        ? throw new Exception("No builder found for the version specified in the document markup.")
        : builder.CreateComponent(layout, this);
}
```

Three steps:

1. Parse the XML.
2. Look up the builder for `<sundex version="...">`.
3. Hand over to `builder.CreateComponent(document, this)`.

The `GetAlternateLookup<ReadOnlySpan<char>>()` is the [[../Engine/Asset Management|`Dictionary` span-key trick]] reused throughout the engine — saves an allocation when the version string is already a span.

### `RegisterBuilder` — versioning

```csharp
public void RegisterBuilder(string version, IComponentBuilder builder)
{
    if (!ComponentBuilderVersions.TryAdd(version, builder))
        throw new Exception("A builder with the same version already exists.");
}
```

Future-proofing: when the markup format changes incompatibly, register a new builder under `"2.0"`. Old documents with `version="1.0"` keep working through `ComponentBuilderV1`; new documents declare `version="2.0"`.

`TryAdd` rather than overwrite — duplicate registrations are an error. The host owns the registry.

### `RegisterElementFactory` — custom tags

```csharp
public void RegisterElementFactory(string tagName, Func<UIContext, UIElement> factory)
{
    if (!ElementFactories.TryAdd(tagName, factory))
        throw new Exception($"A factory for tag '{tagName}' is already registered.");
}

public UIElement? CreateElement(string tagName)
{
    var lookup = ElementFactories.GetAlternateLookup<ReadOnlySpan<char>>();
    return lookup.TryGetValue(tagName, out var factory) ? factory(UIContext) : null;
}
```

This is how applications add their own tags. Example:

```csharp
sundexContext.RegisterElementFactory("waveform",
    ctx => new WaveformView(ctx) { /* defaults */ });
```

Now `<waveform/>` in markup creates a `WaveformView`. The factory runs once per occurrence.

The builder checks custom factories before falling back to "import a foreign component" or throwing — see [[Phases/Parsing Markup|Parsing Markup]] for the full lookup order.

### Component registration

```csharp
public ISundexComponent ResolveComponent(ReadOnlySpan<char> dependency)
{
    var lookup = LoadedComponents.GetAlternateLookup<ReadOnlySpan<char>>();
    return lookup.TryGetValue(dependency, out var component)
        ? component
        : throw new Exception($"Unable to find component: {dependency}");
}

public void RegisterComponent(ISundexComponent component)
{
    if (component.Name == null) throw new Exception("Component name cannot be null.");
    LoadedComponents.Add(component.Name, component);
}
```

The `imports="['x']"` attribute on a `<sundex>` root resolves through `ResolveComponent`. This is what lets one markup document re-use another:

```xml
<!-- header.smxl: defines a "header" component -->
<sundex component="header" implements="header_interface">
    <layout>...</layout>
</sundex>

<!-- main.smxl: imports header -->
<sundex imports="['header']">
    <layout>
        <flex direction="vertical">
            <header/>             <!-- expanded to header.smxl's tree -->
            <label value="Body"/>
        </flex>
    </layout>
</sundex>
```

The builder calls `HandleImplements` on a component whose `<sundex>` has both a `component="..."` name and an `implements="..."` value — that fires `RegisterComponent`, adding it to the registry under its name. From then on, other documents can import it.

### `RunLogicAndVerify` — defensive script execution

```csharp
public void RunLogicAndVerify(SundexComponent component, params ReadOnlySpan<Func<object?>> objectGetters)
{
    // 1. Snapshot caller-supplied "must remain non-null" property getters
    // 2. Find a target object from the first non-null getter.Target
    // 3. Run the logic
    // 4. Re-check the getters — anything that was non-null and is now null → throw
    // 5. Reflect over the target's [SetFromLogic]-decorated members:
    //       - if any are still null → throw
    //       - if any are non-public → throw
}
```

This is the "run the script *and* verify it did what it was supposed to" entry point. Used when the host has a class with `[SetFromLogic]`-tagged properties that must be wired up by the script — calling `RunLogicAndVerify` instead of `RunLogic` directly catches incomplete scripts at load time rather than at first-use.

The full implementation:

```csharp
public void RunLogicAndVerify(SundexComponent component, params ReadOnlySpan<Func<object?>> objectGetters)
{
    var previousValuesRent = ArrayPool<object?>.Shared.Rent(objectGetters.Length);
    try {
        var previousValues = previousValuesRent.AsSpan()[..objectGetters.Length];
        object? target = null;
        for (var index = 0; index < objectGetters.Length; index++) {
            var obj = objectGetters[index].Invoke();
            previousValues[index] = obj;
            if (target == null && objectGetters[index].Target != null)
                target = objectGetters[index].Target;
        }

        component.RunLogic?.Invoke(target ?? this);

        for (var i = 0; i < objectGetters.Length; i++)
            if (objectGetters[i].Invoke() is null && previousValues[i] is not null)
                throw new Exception($"Object getter ... returned null but was expecting a non-null value.");

        if (target == null) return;
        var type = target.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

        // Verify [SetFromLogic] public members are non-null
        foreach (var p in type.GetProperties(flags))
            if (p.GetCustomAttribute<SetFromLogicAttribute>() != null && p.GetValue(target) == null)
                throw new Exception($"Property {p.Name} marked [SetFromLogic] but remains null.");
        foreach (var f in type.GetFields(flags))
            if (f.GetCustomAttribute<SetFromLogicAttribute>() != null && f.GetValue(target) == null)
                throw new Exception($"Field {f.Name} marked [SetFromLogic] but remains null.");

        // Catch [SetFromLogic] applied to non-public members (compile-time mistake)
        const BindingFlags nonPublicFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (var p in type.GetProperties(nonPublicFlags))
            if (p.GetCustomAttribute<SetFromLogicAttribute>() != null)
                throw new Exception($"Property {p.Name} marked [SetFromLogic] but is not public.");
        // ... fields likewise ...
    }
    finally {
        ArrayPool<object?>.Shared.Return(previousValuesRent);
    }
}
```

The `ArrayPool<object?>.Shared.Rent(...)` is the standard pattern for stack-bounded array buffers — avoids the GC pressure of `new object?[N]` for every load. The pooled array is sliced down to `objectGetters.Length` because `Rent` may return an oversized buffer.

The `getter.Target` trick: `Func<object?>` lambdas closing over a member access have their containing object as `Target`. So `() => myHost.SomeProp` has `Target = myHost`. This lets `RunLogicAndVerify` discover the host object without the caller having to pass it twice.

## `[SetFromLogic]` — declarative wiring contract

```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
[MeansImplicitUse]
public class SetFromLogicAttribute : Attribute;
```

A marker. Decorate a public property or field on the host class:

```csharp
public class SettingsHost
{
    public SundexComponent Component { get; set; } = null!;

    [SetFromLogic] public Button SaveButton { get; set; } = null!;
    [SetFromLogic] public Label  TitleLabel { get; set; } = null!;
}
```

Now write a logic script that populates them:

```xml
<logic language="csharp">
    var host = As&lt;SettingsHost&gt;(Context);
    host.SaveButton = Component.GetID&lt;Button&gt;("save_btn");
    host.TitleLabel = Component.GetID&lt;Label&gt;("title");
</logic>
```

Calling `context.RunLogicAndVerify(component, () => host.SaveButton, () => host.TitleLabel)` will:

- Run the script.
- Verify `host.SaveButton` and `host.TitleLabel` are now non-null.
- Reflectively scan `SettingsHost` for *all* `[SetFromLogic]`-decorated members and verify the script set them.

This catches "I added a `[SetFromLogic]` field but forgot to wire it up in the script" at the load site rather than later when something tries to call `SaveButton.OnClick = ...` on null. The `[MeansImplicitUse]` JetBrains annotation tells ReSharper/Rider not to warn about the field being assigned only via reflection.

## Threading

- **`SundexContext.NewComponent`** — runs on whichever thread calls it. Internally it touches GL state (allocates `UIElement`s, `TextBuffer`s) so it must be on the GL thread. The recommended pattern is to parse off-thread and round-trip:

  ```csharp
  ThreadRunner.RunTask(() => {
      var markup = File.ReadAllText(path);
      Game.Enqueue(_ => {
          var c = sundexContext.NewComponent(markup);   // GL thread now
          scene.Root.AddChild(c.Element);
      });
  });
  ```

- **`RegisterBuilder` / `RegisterElementFactory` / `RegisterComponent`** — should happen during scene setup, on the GL thread, before any `NewComponent` calls.

- **`RunLogic` / `RunLogicAndVerify`** — Roslyn's `RunAsync(...).GetAwaiter().GetResult()` blocks the calling thread. The script body itself runs synchronously; if it touches GL state it must be on the GL thread.

## Related

- [[Markup Parser|Markup Parser]] — what produces the `SundexDocument` that `NewComponent` builds.
- [[Phases/Component Builders|Component Builders]] — how the document becomes a component.
- [[Phases/Parsing Logic|Parsing Logic]] — what `RunLogic` actually invokes.
- [[../Components/Abstractions#UIContext|UIContext]] — the rendering context every component shares.
