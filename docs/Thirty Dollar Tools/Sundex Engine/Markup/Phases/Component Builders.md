# Component Builders

`IComponentBuilder` is the interface; `ComponentBuilderV1` is the only implementation. The builder takes a [[../Markup Parser|`SundexDocument`]] and turns it into a [[../Component Definition|`SundexComponent`]] — the orchestrator of the pipeline phases.

> Source: `Sundex/Sundex.Markup/Abstract/IComponentBuilder.cs`, `Sundex/Sundex.Markup/Builders/ComponentBuilderV1.cs`.

## `IComponentBuilder`

```csharp
public interface IComponentBuilder
{
    public SundexComponent CreateComponent(SundexDocument layout, ISundexContext context);
}
```

A single method. Takes the parsed document and the context to build into; returns the realised component. Anything else lives on the implementation.

The interface exists for **versioning** — different markup format versions get different builders, registered in `SundexContext.ComponentBuilderVersions`:

```csharp
public Dictionary<string, IComponentBuilder> ComponentBuilderVersions { get; } = new() {
    { ComponentBuilderV1.Version, new ComponentBuilderV1() }
};
```

When a future v2 markup spec arrives, register `ComponentBuilderV2` under `"2.0"`. Existing `version="1.0"` documents keep working through V1.

## `ComponentBuilderV1.CreateComponent` — the shell

```csharp
public class ComponentBuilderV1 : IComponentBuilder
{
    public const string Version = "1.0";

    public SundexComponent CreateComponent(SundexDocument layout, ISundexContext context)
    {
        var root = layout.Root;

        // 1. Resolve <sundex imports="...">
        List<ISundexComponent>? dependencies = null;
        if (root.Imports.Count > 0)
            dependencies = HandleDependencies(layout, context);

        // 2. Build the SundexNode tree from <layout>
        var tree = layout.Layout.BuildTree();

        // 3. Parse <style> if present
        StyleSheet? styleSheet = null;
        if (layout.Style is not null) {
            // ... resolve src= via AssetProvider, parse via StyleParser, wrap in StyleSheet ...
        }

        // 4. Validate one root, then realise the tree
        if (tree.Count != 1) throw new Exception("Only one root element is supported");
        var rootNode = tree[0];
        var registeredIds     = new Dictionary<string, UIElement>(StringComparer.Ordinal);
        var registeredClasses = new Dictionary<string, List<UIElement>>(StringComparer.Ordinal);

        var uiElement = BuildUIElement(rootNode, context, dependencies, styleSheet,
            registeredIds, registeredClasses);

        // 5. Apply the stylesheet to the realised tree
        if (styleSheet is not null)
            uiElement.ApplyStyleSheet(styleSheet);

        // 6. Construct the SundexComponent
        var component = new SundexComponent {
            Version = Version,
            Context = context,
            Element = uiElement,
            RegisteredIDs = registeredIds,
            RegisteredClasses = registeredClasses
        };

        // 7. Compile <logic> if present
        var logic = layout.Logic;
        Action<object?>? runLogic = null;
        if (logic is not null) {
            // ... look up language plugin, compile, store delegate ...
        }
        component.RunLogic = runLogic;

        // 8. Register if this document declares "implements"
        if (root.Implements?.Length > 0)
            HandleImplements(component, context);

        return component;
    }
}
```

The shape is **straight-line, non-defensive** — each step throws if the input is malformed rather than carrying nullable state forward.

### Step 1 — `HandleDependencies`

```csharp
private static List<ISundexComponent> HandleDependencies(SundexDocument layout, ISundexContext context)
{
    return layout.Root.Imports
        .Select(import => context.ResolveComponent(import))
        .ToList();
}
```

For every name in `imports="['x', 'y']"`, ask the context to find a registered component. Throws if any name is unknown. The list is passed to `BuildUIElement` and consulted when an unknown tag is encountered (see [[Parsing Markup#Tag dispatch|Tag dispatch]]).

### Step 2 — Tree construction

```csharp
var tree = layout.Layout.BuildTree();
if (tree.Count != 1)
    throw new Exception("Only one root element is supported");
```

`BuildTree` returns the immediate children of `<layout>` as `SundexNode`s. The `<layout>` tag is not itself a node — it's just a container marker. Exactly **one** root child is required.

A common mistake: writing `<layout><label/><label/></layout>` and getting "Only one root element is supported." Wrap in a panel.

### Step 3 — Stylesheet preprocessing (if `<style>` exists)

```csharp
StyleSheet? styleSheet = null;
if (layout.Style is not null) {
    var src = layout.Style.SrcLocation;
    if (!string.IsNullOrEmpty(src)) {
        var newSource = context.UIContext.AssetProvider.Load<StringAsset, StringInfo>(
            StringInfo.CreateFromUnknownStorage(src));
        layout.Style.UpdateSourceCode(newSource.Value);
    }

    var styleSheetHolder = StyleParser.Parse(layout.Style.SourceCode, path => {
        var assetStream = context.UIContext.AssetProvider.Load<AssetStream, AssetInfo>(new AssetInfo {
            Location = path
        });
        using var assetString = assetStream.Stream;
        using var stringReader = new StreamReader(assetString);
        return stringReader.ReadToEnd();
    });

    styleSheet = new StyleSheet(styleSheetHolder);
}
```

Three things happen:

1. If the style has `src="..."`, load the file via [[../../Engine/Asset Management|`AssetProvider.Load<StringAsset>`]] and overwrite `SourceCode`.
2. Hand the source text to [[../../Style DSL/Style DSL|`StyleParser.Parse`]] with an *import resolver* — a callback `(string path) => string content` that the parser can call when it sees `@import "...";` directives. The resolver delegates to `AssetProvider`.
3. Wrap the parsed result in a `StyleSheet` (the matching engine — selector → matched values).

The import resolver is **closing over the context's `AssetProvider`**, so imported stylesheets resolve through the same asset paths as the source. This is what lets a stylesheet split across files (`@import "theme.smxs"; @import "buttons.smxs";`) without the parser needing to know about file I/O.

`StringInfo.CreateFromUnknownStorage(src)` is the asset-locator helper — tries embedded, on-disk, and cache locations in turn.

### Step 4 — `BuildUIElement` recursion

```csharp
var registeredIds     = new Dictionary<string, UIElement>(StringComparer.Ordinal);
var registeredClasses = new Dictionary<string, List<UIElement>>(StringComparer.Ordinal);

var uiElement = BuildUIElement(rootNode, context, dependencies, styleSheet,
    registeredIds, registeredClasses);
```

Two empty dictionaries get passed by reference into `BuildUIElement`, which populates them as it walks the tree. By the end, every `id="..."` and `class="..."` in the markup has an entry. They're then handed to the `SundexComponent` so the host can call `GetID<T>("save_btn")`.

`StringComparer.Ordinal` — case-sensitive, no culture rules. IDs and classes are identifiers, not natural-language text.

The full `BuildUIElement` body is the subject of [[Parsing Markup|Parsing Markup]].

### Step 5 — Apply stylesheet

```csharp
if (styleSheet is not null)
    uiElement.ApplyStyleSheet(styleSheet);
```

`UIElement.ApplyStyleSheet` walks the realised tree, looking for selector matches and applying values via the `[NamedSetting]` reflection machinery (see [[../../Components/Abstractions#Style application — the [NamedSetting] flow|UIElement style application]]).

This **must** happen after the tree is built — `Panel.ApplyStyleSheet` calls `child.ApplyStyleSheet` recursively, so children must exist. It can't be folded into `BuildUIElement` because some style values (e.g. background swaps) need the parent's `Computed.Width`/`Height` to resolve `border-radius`, which hasn't run a layout yet — but at least the tree shape is stable.

### Step 6 — Component construction

```csharp
var component = new SundexComponent {
    Version = Version,
    Context = context,
    Element = uiElement,
    RegisteredIDs = registeredIds,
    RegisteredClasses = registeredClasses
};
```

A simple struct-init. `Dependencies` and `Children` are left at their default empty values; future versions might propagate them.

### Step 7 — Logic compilation

```csharp
var logic = layout.Logic;
Action<object?>? runLogic = null;

if (logic is not null) {
    LanguageProvider.Languages.TryGetValue(logic.Language, out var language);
    if (language is null)
        throw new NotSupportedException($"Language {logic.Language} is not supported.");

    if (!string.IsNullOrEmpty(logic.SrcLocation)) {
        var newSource = context.UIContext.AssetProvider.Load<StringAsset, StringInfo>(
            StringInfo.CreateFromUnknownStorage(logic.SrcLocation));
        logic.UpdateSourceCode(newSource.Value);
    }

    runLogic = language.Compile(logic.SourceCode, context, component, logic.LanguageImports);
}

component.RunLogic = runLogic;
```

Symmetric with the style stage — same `src=` resolution, same `UpdateSourceCode`, then hand off to a language plugin (`LanguageProvider.Languages["csharp"]`) to compile.

The compiled delegate is **stored, not invoked**. The host calls `component.RunLogic?.Invoke(target)` when ready (typically after wiring the component into the scene). See [[Parsing Logic|Parsing Logic]] for the C# specifics.

The key subtlety: `language.Compile` receives the freshly-built `component` so the script can capture it via its `ScriptGlobals.Component` member. Scripts close over `component` and can call `component.GetID<T>("...")`.

### Step 8 — `HandleImplements`

```csharp
private static void HandleImplements(SundexComponent component, ISundexContext context)
{
    context.RegisterComponent(component);
}

// in CreateComponent:
if (root.Implements?.Length > 0)
    HandleImplements(component, context);
```

If the document has `<sundex implements="some_interface">`, register the component in the context's `LoadedComponents` dictionary under its `Name`. From then on, other documents importing this name (`imports="['name']"`) will resolve to this component.

`Implements` is currently free-form — there's no actual interface conformance check. The string serves as documentation: "this component is meant to fulfil the role of `some_interface`." Future builder versions could add structural validation.

A document **without** `implements` won't be registered even if it has a `component="name"` — registration requires both. This is so that helper components can have names for diagnostics without polluting the import namespace.

## Threading

`CreateComponent` is GL-thread only because:

- It instantiates `Label`/`TextBuffer`/etc., which allocate GPU resources.
- `AssetProvider.Load` calls in steps 3 and 7 may queue GL operations.
- Roslyn's `script.Compile()` is CPU-only but `script.RunAsync(...).GetAwaiter().GetResult()` (later, when `RunLogic` fires) typically wants the GL thread because scripts touch UI elements.

Production code that loads markup async-ly should:

```csharp
ThreadRunner.RunTask(() => {
    var markup = File.ReadAllText(path);    // off-thread
    Game.Enqueue(_ => {                     // GL thread
        var component = sundexContext.NewComponent(markup);
        scene.Root.AddChild(component.Element);
        component.RunLogic?.Invoke(host);
    });
});
```

There's no good way to move `CreateComponent` itself off-thread — too many GL allocations.

## Related

- [[Parsing Markup|Parsing Markup]] — the `BuildUIElement` recursion this orchestrator drives.
- [[Parsing Style|Parsing Style]] — what step 3 / step 5 actually do.
- [[Parsing Logic|Parsing Logic]] — what step 7 does.
- [[../Component Definition|Component Definition]] — the `SundexComponent` this produces.
- [[../../Engine/Asset Management|Asset Management]] — the `src=` resolution backing.
