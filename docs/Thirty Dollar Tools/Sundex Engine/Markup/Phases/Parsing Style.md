# Parsing Style

The bridge between the markup pipeline and the [[../../Style DSL/Style DSL|Style DSL]]. The builder loads `<style>` content (inline or from `src=`), hands it to `StyleParser.Parse` with an import resolver, wraps the result in a `StyleSheet`, and later calls `uiElement.ApplyStyleSheet(...)` to fan styles out across the realised tree.

> Source: `Sundex/Sundex.Markup/Builders/ComponentBuilderV1.cs` (the style block in `CreateComponent`).
>
> Source for `StyleParser` itself: see [[../../Style DSL/Style DSL|Style DSL]].

## The two-step pattern

```
   <style src="x.smxs"/>            <style>...inline css-like syntax...</style>
        │                                    │
        │  AssetProvider.Load                │  (already in SourceCode)
        ▼                                    ▼
   StyleContainer.SourceCode  ◄──────────────┘
        │
        │  StyleParser.Parse(source, importResolver)
        ▼
   styleSheetHolder            (raw parsed AST)
        │
        │  new StyleSheet(holder)
        ▼
   styleSheet                  (matching engine)
        │
        │  uiElement.ApplyStyleSheet(styleSheet)
        ▼
   every UIElement gets its [NamedSetting] properties written
```

## The relevant block in `CreateComponent`

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
        using var assetString  = assetStream.Stream;
        using var stringReader = new StreamReader(assetString);
        return stringReader.ReadToEnd();
    });

    styleSheet = new StyleSheet(styleSheetHolder);
}
// ... later, after BuildUIElement:
if (styleSheet is not null)
    uiElement.ApplyStyleSheet(styleSheet);
```

Three things to notice.

### 1. `src=` resolution

```csharp
var src = layout.Style.SrcLocation;
if (!string.IsNullOrEmpty(src)) {
    var newSource = context.UIContext.AssetProvider.Load<StringAsset, StringInfo>(
        StringInfo.CreateFromUnknownStorage(src));
    layout.Style.UpdateSourceCode(newSource.Value);
}
```

If `<style src="theme.smxs"/>`, the builder loads `theme.smxs` via the [[../../Engine/Asset Management|`AssetProvider`]] and rewrites the container's `SourceCode`. From here on, the inline-vs-external distinction is invisible — both paths land in `layout.Style.SourceCode`.

`StringInfo.CreateFromUnknownStorage` is the asset-locator helper. It tries:
1. The application's embedded resources (compiled into the assembly).
2. The on-disk asset folder.
3. The asset cache.

Returns the first match. This way the same `src="theme.smxs"` works in development (on-disk) and production (embedded).

### 2. The import resolver

```csharp
var styleSheetHolder = StyleParser.Parse(layout.Style.SourceCode, path => {
    var assetStream = context.UIContext.AssetProvider.Load<AssetStream, AssetInfo>(new AssetInfo {
        Location = path
    });
    using var assetString  = assetStream.Stream;
    using var stringReader = new StreamReader(assetString);
    return stringReader.ReadToEnd();
});
```

The second parameter to `StyleParser.Parse` is a `Func<string, string>` — the parser calls it whenever it sees `@import "...";` directives in the stylesheet. The argument is the path the directive specified; the return is the resolved source text.

The closure captures `context.UIContext.AssetProvider`. This means **imported stylesheets resolve through the same asset paths** as the source — `@import "buttons.smxs"` works the same way `<style src="buttons.smxs"/>` would.

`AssetStream` (rather than `StringAsset`) is used here to return a stream that's read inline. This avoids caching the imported text in the `AssetProvider`'s string cache when it's only needed once.

### 3. `StyleSheet` wraps the holder

```csharp
styleSheet = new StyleSheet(styleSheetHolder);
```

`StyleParser.Parse` returns a "holder" — a raw AST of selectors, rules, and values. `StyleSheet` wraps it with the matching engine: given a tag/ID/classes triple, return all matching values. See [[../../Style DSL/Style DSL|Style DSL]] for the internals.

## When `ApplyStyleSheet` runs

`uiElement.ApplyStyleSheet(styleSheet)` is called **after** `BuildUIElement` returns:

```csharp
var uiElement = BuildUIElement(rootNode, ...);
if (styleSheet is not null)
    uiElement.ApplyStyleSheet(styleSheet);
```

This timing is critical. `Panel.ApplyStyleSheet` recurses:

```csharp
public override void ApplyStyleSheet(StyleSheet styleSheet)
{
    base.ApplyStyleSheet(styleSheet);
    foreach (var child in Children) child.ApplyStyleSheet(styleSheet);
}
```

So every element in the tree gets a chance to match against the stylesheet. The `base.ApplyStyleSheet` (in `UIElement`) is what does the reflection over `[NamedSetting]` properties — see [[../../Components/Abstractions#Style application — the [NamedSetting] flow|UIElement style application]] for the gory details.

### Why not during `BuildUIElement`?

You *could* apply the stylesheet to each element as it's constructed. The reason it happens after:

1. **Selectors with descendant combinators** (e.g. `panel label { ... }`) need the parent–child relationships to be in place. During `BuildUIElement`, the parent isn't fully assembled yet — its children list is still being populated.
2. **Style state snapshots** (the `_baseSnapshot` for restoring `:hover` overrides) are taken in `ApplyStyleSheet`. Doing this once after the tree is stable is cleaner than redoing it as the tree grows.
3. **Pre-emptive style peeks** for `<progress>` and `<button>` already grab the relevant background values during construction (see [[Parsing Markup#ExtractBackgroundStyle|Parsing Markup]]). Anything else is fine to defer.

## Relationship to the inline pattern

Two equivalent ways to write the same stylesheet:

```xml
<!-- Inline -->
<sundex>
    <layout>...</layout>
    <style>
        button { background: rgb(50, 50, 50); }
        button:hover { background: rgb(70, 70, 70); }
    </style>
</sundex>
```

vs.

```xml
<!-- External -->
<sundex>
    <layout>...</layout>
    <style src="buttons.smxs"/>
</sundex>
```

Where `buttons.smxs` contains:

```css
button { background: rgb(50, 50, 50); }
button:hover { background: rgb(70, 70, 70); }
```

The builder doesn't care which form was used — by the time `StyleParser.Parse` runs, `SourceCode` is set in both cases. External is preferred for any non-trivial stylesheet because:

- Editors syntax-highlight `.smxs` correctly (XML-mixed CSS-like syntax confuses most editors).
- Multiple components can share the same external stylesheet.
- `src=` paths resolve through the asset cache, which means hot-reloads work.

## Optional vs missing

`<style>` is **optional**. If the document has no `<style>` element:

```csharp
StyleSheet? styleSheet = null;
if (layout.Style is not null) { ... }
// ...
if (styleSheet is not null)
    uiElement.ApplyStyleSheet(styleSheet);
```

Both blocks are skipped. The realised tree gets only the attribute-applied values from `ApplyAttributes` — no styled colours, no fonts, no border radii. Useful for simple test cases or when styling is added later via `Element.ApplyStyleSheet(...)` directly.

A common pattern is **shared stylesheets**: register a stylesheet once at scene init and apply it to every component as it's built:

```csharp
var theme = StyleParser.Parse(File.ReadAllText("theme.smxs"), resolver);
var themeSheet = new StyleSheet(theme);

// Later, each component built without a <style> section gets:
component.Element.ApplyStyleSheet(themeSheet);
```

Here the markup pipeline doesn't apply the theme — the host does. The theme is invisible to `ComponentBuilderV1`.

## Threading

`StyleParser.Parse` is pure CPU — no GL, no I/O directly (only through the resolver callback). Could in principle be off-thread.

`AssetProvider.Load<StringAsset>` may queue GL upload operations (for images referenced by stylesheets, e.g. background images). These get drained on the GL thread later, so calling `Load` from off-thread is correct as long as you don't try to use the resource before its GL resources are uploaded.

`uiElement.ApplyStyleSheet` is GL-thread only — it can swap `Renderable`s in the render queue (via `HandleRenderableSwap`), which mutates `UIContext.LayeredRenderQueue`.

## Related

- [[../../Style DSL/Style DSL|Style DSL]] — the language inside `<style>`, including `StyleParser.Parse` and `StyleSheet`.
- [[Component Builders|Component Builders]] — the orchestrator that runs this phase.
- [[Parsing Markup|Parsing Markup]] — the parallel phase for layout.
- [[../../Components/Abstractions#Style application — the [NamedSetting] flow|UIElement.ApplyStyleSheet]] — what gets called at the end.
- [[../../Engine/Asset Management|Asset Management]] — the `src=` resolution backing.
