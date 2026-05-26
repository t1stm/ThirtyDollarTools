# Import

The `import "..."` directive — load and merge another `.snx.ss` file into the current one. Implements file-include semantics with cycle protection.

> Source: `Sundex/Sundex.Style.DSL/StyleParser.cs` (`ParseImport`), `Sundex/Sundex.Style.DSL/StyleSheetHolder.cs` (`Merge`).

## Syntax

```css
import "Sundex.Style.DSL/Examples/default.snx.ss";

// the imported file's animations, components, classes, and IDs
// are now in scope and may be redefined or extended:

@component button {       // full override of imported button
    background = "#ff0000";
    padding = 20px;
}

component label {         // merged with imported label
    background = "#ffffff";
}
```

`import` statements can appear anywhere at the top level — start of file, between blocks, end of file. Order matters for override semantics (see below).

## `ParseImport` — the recursion

```csharp
private void ParseImport(StyleSheetHolder sheetHolder)
{
    SkipWhitespaceAndComments();
    var path = ReadString();
    SkipWhitespaceAndComments();
    if (Check(';')) Advance();

    if (fileLoader == null) return;

    if (!_importedPaths.Add(path)) return;
    var importedDsl = fileLoader(path);

    var importedParser = new StyleParser(importedDsl, fileLoader, _importedPaths);
    var importedSheet = importedParser.ParseSheet();
    sheetHolder.Merge(importedSheet);
}
```

Five steps:

1. **Read the path** — `ReadString` returns the inside of `"..."`. Trailing `;` is optional.
2. **Bail if no `fileLoader`** — without a callback, imports become no-ops.
3. **Cycle check** — `_importedPaths.Add(path)` returns `false` if the path has been seen.
4. **Recursive parse** — new `StyleParser` over the imported source, **sharing** the `_importedPaths` set so transitive imports are also tracked.
5. **Merge** the result into the current `sheetHolder`.

### `if (fileLoader == null) return;`

If the parser was constructed without a `fileLoader`, every import is silently ignored. The directive is parsed (the `;` is consumed), then dropped on the floor.

This is the case for the static `StyleParser.Parse(dsl)` overload — no resolver, so imports don't work. The only place that **does** pass a `fileLoader` is the markup pipeline, in [[../Markup/Phases/Parsing Style|Parsing Style]]:

```csharp
var styleSheetHolder = StyleParser.Parse(layout.Style.SourceCode, path => {
    var assetStream = context.UIContext.AssetProvider.Load<AssetStream, AssetInfo>(new AssetInfo {
        Location = path
    });
    using var assetString = assetStream.Stream;
    using var stringReader = new StreamReader(assetString);
    return stringReader.ReadToEnd();
});
```

So in practice, imports work when the stylesheet is loaded via the markup pipeline. They don't work in tests that call `StyleParser.Parse(dsl)` without a resolver.

## Cycle protection

```csharp
private readonly HashSet<string> _importedPaths = importedPaths ?? [];
...
if (!_importedPaths.Add(path)) return;
```

The `_importedPaths` set is **shared across recursive parsers** via the constructor's third argument:

```csharp
var importedParser = new StyleParser(importedDsl, fileLoader, _importedPaths);
```

So if `a.snx.ss` imports `b.snx.ss` which imports `a.snx.ss`, the second `import "a.snx.ss"` returns immediately — no infinite recursion.

The first parser starts with an empty set (defaulted via `importedPaths ?? []`). The set grows as each unique path is processed.

### Path identity

The cycle check is on the **literal path string** — `import "a.snx.ss"` and `import "./a.snx.ss"` would be considered different paths even if they resolve to the same file. This is rarely a problem because production stylesheets typically use a consistent path convention.

The `fileLoader` callback in [[../Markup/Phases/Parsing Style|Parsing Style]] resolves through `AssetProvider`, which itself does some path normalisation, but the cycle check happens **before** the loader is called — so the parser's view of "have I seen this path?" is purely string-based.

## `StyleSheetHolder.Merge`

```csharp
public void Merge(StyleSheetHolder other)
{
    MergeDictionary(Animations, other.Animations);
    MergeDictionary(Components, other.Components);
    MergeDictionary(Classes, other.Classes);
    MergeDictionary(IDTags, other.IDTags);
    foreach (var name in other.FullOverrides) FullOverrides.Add(name);
}

private static void MergeDictionary(Dictionary<string, Dictionary<string, IStyleValue>> target,
    Dictionary<string, Dictionary<string, IStyleValue>> source)
{
    foreach (var kvp in source)
        if (target.TryGetValue(kvp.Key, out var existingProps))
            foreach (var prop in kvp.Value)
                existingProps[prop.Key] = prop.Value;     // property-level merge
        else
            target[kvp.Key] = new Dictionary<string, IStyleValue>(kvp.Value);  // copy
}
```

Two-level merge:

1. **Top-level entries** (animation/component/class/id names) are unioned.
2. **Properties within an entry** are merged with **last-write-wins**.

So if `default.snx.ss` defines:

```css
component button { background = "#000000"; font-size = 14px; }
```

and the importing file then writes:

```css
component button { background = "#ffffff"; }
```

After merge, `Components["button"]` contains:

```
{ "background": ColorValue("#ffffff"),     // overridden
  "font-size":  NumberValue(14, "px") }    // preserved
```

If the importing file uses `@component`:

```css
@component button { background = "#ffffff"; }
```

The `ParseBlock` `isOverride = true` path replaces the entry entirely, **inside the same `StyleSheetHolder`**, after the merge. The order of operations matters:

1. Import → `Merge` adds `default.snx.ss`'s `button` (with both background and font-size).
2. `@component button { ... }` → `ParseBlock` with `isOverride = true` does `target[name] = properties;` — replaces the merged result entirely.

So `@component` is defined **post-import**.

## Order of operations

Imports are parsed **inline** at their position in the file. The flow:

```
Parse current file:
    ├── (top of file)
    ├── See "import" → ParseImport
    │   ├── Recursively parse imported file
    │   └── Merge imported holder into current holder
    ├── See "component foo { ... }" → ParseBlock (merge with current holder)
    ├── See "import" → ParseImport (another file, merged in)
    └── ...
```

So:

```css
component button { background = "#000000"; }    // current file
import "other.snx.ss";                          // imports a button with background = "#ff0000"
```

After parsing, `button.background = "#ff0000"` (the import wins because it ran second and merged property-level on top).

Inversion:

```css
import "other.snx.ss";                          // adds button with background = "#ff0000"
component button { background = "#000000"; }    // current file's redefinition wins
```

Now `button.background = "#000000"`. **Order is significant** — typically you'd put imports **first** so that the current file's definitions override imported ones.

## Cycle protection — what happens if you have one

```css
// a.snx.ss
import "b.snx.ss";
component foo { background = "#ff0000"; }
```

```css
// b.snx.ss
import "a.snx.ss";
component bar { background = "#00ff00"; }
```

Loading `a.snx.ss`:
1. Parser starts, `_importedPaths = {}`.
2. Sees `import "b.snx.ss"`. Adds `"b.snx.ss"` to `_importedPaths` → `{"b.snx.ss"}`.
3. Recursively parses `b.snx.ss` with the **same** `_importedPaths`.
4. In `b.snx.ss`, sees `import "a.snx.ss"`. Tries to add `"a.snx.ss"` → already... no wait, it's not there yet. So it **does** get added.
5. Recursively parses `a.snx.ss` again.
6. In second `a.snx.ss` parse, sees `import "b.snx.ss"`. Tries to add `"b.snx.ss"` → already in set, returns immediately.
7. The second `a.snx.ss` parse continues from after the import → defines `foo`. Returns. Merged into the parent (which is `b.snx.ss`'s parse).
8. `b.snx.ss` continues, defines `bar`. Returns. Merged into the original.
9. Original `a.snx.ss` parse continues → defines `foo` **again**. Property-level merge over what was already there.

End result: `Components` contains both `foo` and `bar`. `foo` is defined twice (once from the recursive re-parse, once from the original file), but property-level merge means it ends up the same.

The cycle is broken — but the recursive re-entry isn't ideal. Best practice: don't write circular imports.

## Threading

`ParseImport` calls `fileLoader` synchronously. If the loader does I/O (file read, asset load), the parse blocks until it completes. This is fine for typical use (small stylesheets, fast asset access).

The whole parse — including imports — is safe to run off the GL thread. Only the `AssetProvider`'s GL-resource queueing happens on the GL thread later, when the resulting stylesheet is applied.

## Default `fileLoader` — `AssetProvider`

The standard import resolver from [[../Markup/Phases/Parsing Style|Parsing Style]]:

```csharp
path => {
    var assetStream = context.UIContext.AssetProvider.Load<AssetStream, AssetInfo>(new AssetInfo {
        Location = path
    });
    using var assetString = assetStream.Stream;
    using var stringReader = new StreamReader(assetString);
    return stringReader.ReadToEnd();
}
```

Resolves `path` through the [[../Engine/Asset Management|`AssetProvider`]], which tries:

1. Embedded resources in the application assembly.
2. On-disk asset folders.
3. The asset cache.

Returns the first hit. This is the same resolution used for `<style src="...">`. So `import "buttons.snx.ss"` and `<style src="buttons.snx.ss"/>` consult identical paths.

`AssetStream` is used (rather than `StringAsset`) so that the loaded text isn't cached in the `AssetProvider`'s string cache — the text is only needed for one-shot parsing.

## Custom `fileLoader` — testing and tooling

For tests or custom tooling, you can pass any `Func<string, string>`:

```csharp
var stylesheets = new Dictionary<string, string> {
    { "theme.snx.ss", "component button { background = \"#000000\"; }" },
    { "main.snx.ss",  "import \"theme.snx.ss\";\ncomponent label { ... }" }
};

var holder = StyleParser.Parse(stylesheets["main.snx.ss"], path => stylesheets[path]);
```

The loader is invoked synchronously per import — no async support. If the import is slow (network fetch?), the parser blocks.

## Limits

- No conditional imports (`@import "..." if condition;`). All imports are unconditional.
- No relative-path resolution at the parser level — all paths are taken at face value and handed to the loader. The loader (or `AssetProvider`) handles relative resolution.
- No partial imports (`import { button } from "..."`) — always whole-file.
- No URL imports — paths must be loader-resolvable strings.

## Related

- [[Blocks|Blocks]] — `@component` full-override interacts with imports.
- [[Style DSL|Style DSL]] — the index.
- [[../Markup/Phases/Parsing Style|Parsing Style]] — where the markup pipeline supplies the file loader.
- [[../Engine/Asset Management|Asset Management]] — what the standard file loader resolves through.
