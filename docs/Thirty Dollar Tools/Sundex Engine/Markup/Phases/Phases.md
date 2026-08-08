# Phases

The "document → realised UI tree" pipeline. There's exactly one builder version today (`ComponentBuilderV1`), and it walks each section of a [`SundexDocument`](../Markup%20Parser.md) through three sub-phases:

1. **[Component Builders](Component%20Builders.md)** — `IComponentBuilder` and the dispatch shell of `ComponentBuilderV1.CreateComponent`. The orchestrator that runs the other phases in order.
2. **[Parsing Markup](Parsing%20Markup.md)** — `BuildUIElement` and `ApplyAttributes`. Turns a `SundexNode` tree into a `UIElement` tree with the right tags, IDs, classes, and attributes.
3. **[Parsing Style](Parsing%20Style.md)** — runs `StyleParser.Parse` on the inline-or-loaded `<style>` source, then calls `uiElement.ApplyStyleSheet`.
4. **[Parsing Logic](Parsing%20Logic.md)** — looks up the language plugin, compiles via Roslyn (for C#), wraps in an `Action<object?>` delegate stored on `SundexComponent.RunLogic`.

> Source: `Sundex/Sundex.Markup/Builders/`, `Sundex/Sundex.Markup/Logic/`.

## Why split the pipeline this way?

Each phase has a different *responsibility* and a different *cost profile*:

- **Markup phase** — fast, GL-touching (allocates `TextBuffer`s, `Renderable`s). Has to be on the GL thread.
- **Style phase** — pure text → AST conversion + reflective property application. Fast and GL-touching (when stylesheets create `Renderable`s for backgrounds).
- **Logic phase** — slow (Roslyn compilation can take ~100ms+ for large scripts). Pure CPU until the script *runs*. The compilation can be moved off-thread; running the script must be on the GL thread.

Splitting the phases makes it possible to selectively cache, defer, or fail one without affecting the others.

## Order of operations

```
ComponentBuilderV1.CreateComponent(document, context)
    │
    ├── 1. HandleDependencies              ← resolve <sundex imports="...">
    │
    ├── 2. document.Layout.BuildTree       ← <layout> XML → SundexNode tree
    │
    ├── 3. Parse <style> (if present)
    │       ├── load src= via AssetProvider
    │       └── StyleParser.Parse(srcCode, importResolver)  → StyleSheet
    │
    ├── 4. BuildUIElement (recursive)
    │       ├── tag dispatch (case "label", "panel", ...)
    │       ├── ApplyAttributes
    │       └── populate registeredIds / registeredClasses
    │
    ├── 5. uiElement.ApplyStyleSheet(styleSheet)   ← if styleSheet != null
    │
    ├── 6. Construct SundexComponent
    │
    ├── 7. Compile <logic> (if present)
    │       ├── load src= via AssetProvider
    │       ├── LanguageProvider.Languages[language]
    │       └── language.Compile(src, context, component, imports) → Action<object?>
    │       (assigned to component.RunLogic)
    │
    └── 8. RegisterComponent (if component="..." or implements="...")
            └── skipped when rebuilding an imported component at a usage site
```

Two important non-obvious bits:

1. **Style is parsed *before* the tree is built.** That's because `BuildUIElement` peeks at the stylesheet for some tags (`<progress>` reads `background`/`foreground` style values to construct child panels with the right defaults — see `ExtractBackgroundStyle` in [Parsing Markup](Parsing%20Markup.md)).

2. **`ApplyStyleSheet` happens after the tree exists.** Even though the stylesheet was *parsed* early, the actual style application needs every `UIElement` to exist so it can recurse — `Panel.ApplyStyleSheet` walks `Children`, etc.

3. **Logic runs *last*, but doesn't fire until something invokes `component.RunLogic`.** Compile is during `CreateComponent`; execution is deferred. The host calls `RunLogic` (or `RunLogicAndVerify`) when it's ready.

## What each sub-page covers

- **[Component Builders](Component%20Builders.md)** — the `IComponentBuilder` interface, the version dispatch in `SundexContext`, and the high-level shape of `ComponentBuilderV1.CreateComponent`. Entry point.
- **[Parsing Markup](Parsing%20Markup.md)** — the `BuildUIElement` switch, custom factory dispatch, `ApplyAttributes`, the `ParseLiteralOrComputable` helper, ID/class registration. The largest sub-phase.
- **[Parsing Style](Parsing%20Style.md)** — how `<style src="...">` resolves through `AssetProvider`, how the import resolver works, where `StyleSheet` plugs into `UIElement.ApplyStyleSheet`. Bridge to the [Style DSL](../../Style%20DSL/Style%20DSL.md).
- **[Parsing Logic](Parsing%20Logic.md)** — `LanguageProvider`, the abstract `SundexScript`, the `CSharp` Roslyn implementation, `ScriptGlobals`, and the `As<T>` helper.

## Related

- [Markup Parser](../Markup%20Parser.md) — the previous stage; produces the `SundexDocument` these phases consume.
- [Component Definition](../Component%20Definition.md) — what these phases produce.
- [Components](../../Components/Components.md) — the realised types.
- [Style DSL](../../Style%20DSL/Style%20DSL.md) — what the style sub-phase delegates to.
