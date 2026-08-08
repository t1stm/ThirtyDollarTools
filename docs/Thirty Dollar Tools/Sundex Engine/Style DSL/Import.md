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

## Named imports — `as`

```css
import "theme.snx.ss" as theme;
```

The alias scopes the imported file's **variables** and nothing else. `class`, `id`, `component`
and `animation` blocks merge globally exactly as with a plain import; only `$theme.accent`-style
access changes. See [Variables](Variables.md) for the full scoping rules.

## `ParseImport` — the recursion

```csharp
private void ParseImport()
{
    SkipWhitespaceAndComments();
    var path = ReadString();
    SkipWhitespaceAndComments();

    string? alias = null;
    if (Match("as"))
    {
        SkipWhitespaceAndComments();
        alias = ReadIdentifier();
        SkipWhitespaceAndComments();
    }

    if (Check(';')) Advance();

    if (fileLoader == null) return;

    if (!_parsedImports.TryGetValue(path, out var imported))
    {
        // Registered before recursing, so a cycle sees the partial holder instead of looping.
        imported = new StyleSheetHolder();
        _parsedImports[path] = imported;

        var importedParser = new StyleParser(fileLoader(path), fileLoader, _parsedImports);
        importedParser.ParseSheet(imported);
    }

    if (_mergedBlocks.Add(path))
        _sheet.Merge(imported, false);

    if (alias != null)
        _sheet.Namespaces[alias] = imported.Variables;
    else if (_mergedVariables.Add(path))
        foreach (var (name, value) in imported.Variables)
            _sheet.Variables[name] = value;
}
```

Steps:

1. **Read the path** — `ReadString` returns the inside of `"..."`. Trailing `;` is optional.
2. **Read the optional alias** — `as name`, between the path and the `;`.
3. **Bail if no `fileLoader`** — without a callback, imports become no-ops.
4. **Parse once per path** — a cache miss registers an empty holder, *then* recurses into it.
5. **Merge blocks at most once** into this sheet.
6. **Bind variables** — into the namespace if aliased, otherwise into the global variable scope.

## The parse cache

```csharp
private readonly Dictionary<string, StyleSheetHolder> _parsedImports;  // shared with children
private readonly HashSet<string> _mergedBlocks = [];                   // per file
private readonly HashSet<string> _mergedVariables = [];                // per file
```

This replaced the single shared `_importedPaths` set, which conflated two jobs — breaking cycles and merging each file once — and so couldn't express "I already merged this file, but this second directive still needs to bind an alias to it":

```css
import "theme.snx.ss";
import "theme.snx.ss" as theme;   // used to bind an empty alias
```

Now the two jobs are separate:

- **`_parsedImports`** is **shared** down the whole parse tree. It's what makes each file parse once and what breaks cycles. Keyed on the literal path string (see [Path identity](#path-identity)).
- **`_mergedBlocks` / `_mergedVariables`** are **per parser**, i.e. per file. Importing a path twice into one sheet merges it once, which is what preserves the override ordering described below. They must *not* be shared — a nested re-entrant parse would otherwise consume the parent's guard and the parent would silently skip its own merge.

Two separate guards, rather than one, so that an aliased import followed by a plain import of the same path still brings that file's variables into the global scope.

### `if (fileLoader == null) return;`

If the parser was constructed without a `fileLoader`, every import is silently ignored. The directive is parsed (the `;` is consumed), then dropped on the floor.

This is the case for the static `StyleParser.Parse(dsl)` overload — no resolver, so imports don't work. The only place that **does** pass a `fileLoader` is the markup pipeline, in [Parsing Style](../Markup/Phases/Parsing%20Style.md):

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
imported = new StyleSheetHolder();
_parsedImports[path] = imported;        // registered BEFORE the recursive parse
new StyleParser(fileLoader(path), fileLoader, _parsedImports).ParseSheet(imported);
```

The cache entry is inserted **before** recursing, so a file that imports its way back to itself finds an entry and never re-parses. `ParseSheet` takes an optional target holder precisely so the entry can be created up front.

So if `a.snx.ss` imports `b.snx.ss` which imports `a.snx.ss`, the inner `import "a.snx.ss"` finds `a`'s (still partially filled) holder and merges that instead of recursing. No infinite recursion.

The first parser starts with an empty cache (defaulted via `parsedImports ?? new()`), which grows as each unique path is parsed.

### Path identity

The cycle check is on the **literal path string** — `import "a.snx.ss"` and `import "./a.snx.ss"` would be considered different paths even if they resolve to the same file. This is rarely a problem because production stylesheets typically use a consistent path convention.

The `fileLoader` callback in [Parsing Style](../Markup/Phases/Parsing%20Style.md) resolves through `AssetProvider`, which itself does some path normalisation, but the cycle check happens **before** the loader is called — so the parser's view of "have I seen this path?" is purely string-based.

## `StyleSheetHolder.Merge`

```csharp
public void Merge(StyleSheetHolder other, bool includeVariables = true)
{
    MergeDictionary(Animations, other.Animations);
    MergeDictionary(Components, other.Components);
    MergeDictionary(Classes, other.Classes);
    MergeDictionary(IDTags, other.IDTags);
    foreach (var name in other.FullOverrides) FullOverrides.Add(name);
    if (includeVariables)
        foreach (var (name, value) in other.Variables)
            Variables[name] = value;
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

`Variables` merges flat, same last-write-wins rule, and is skipped when the import is aliased. `Namespaces` is **never** merged — an alias belongs to the file that declared it. See [Variables](Variables.md).

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
1. Root parser starts on `a.snx.ss`'s text, `_parsedImports = {}`. Note the **root file has no import path**, so it isn't in the cache.
2. Sees `import "b.snx.ss"`. Cache miss → registers an empty holder for `"b.snx.ss"`, then parses `b.snx.ss` into it.
3. In `b.snx.ss`, sees `import "a.snx.ss"`. Cache miss (the root file was never registered under a path) → registers a holder for `"a.snx.ss"` and parses that text a second time.
4. In this second `a.snx.ss` parse, `import "b.snx.ss"` **hits the cache** — it gets `b`'s holder, which is still empty at this point, and merges nothing. Recursion stops here.
5. The second `a.snx.ss` parse continues → defines `foo` into `a`'s cached holder.
6. Back in `b.snx.ss`: merges `a`'s holder (so `b` now has `foo`), then defines `bar`.
7. Back in the root: merges `b`'s holder → gets both `foo` and `bar`. Then defines `foo` again, property-merged over what's there.

End result: `Components` contains both `foo` and `bar`. `foo` is defined twice (once from the recursive re-parse, once from the original file), but property-level merge means it ends up the same.

The cycle is broken — but the recursive re-entry isn't ideal, and a file caught in a cycle can be merged while only partially parsed. Best practice: don't write circular imports.

## Threading

`ParseImport` calls `fileLoader` synchronously. If the loader does I/O (file read, asset load), the parse blocks until it completes. This is fine for typical use (small stylesheets, fast asset access).

The whole parse — including imports — is safe to run off the GL thread. Only the `AssetProvider`'s GL-resource queueing happens on the GL thread later, when the resulting stylesheet is applied.

## Default `fileLoader` — `AssetProvider`

The standard import resolver from [Parsing Style](../Markup/Phases/Parsing%20Style.md):

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

Resolves `path` through the [`AssetProvider`](../Engine/Asset%20Management.md), which tries:

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
- No partial imports (`import { button } from "..."`) — always whole-file. `as` scopes variables; it doesn't select what gets imported.
- No URL imports — paths must be loader-resolvable strings.
- Aliases don't nest — `$outer.inner.value` isn't a thing, and an alias isn't re-exported to files that import the file declaring it.

## Related

- [Variables](Variables.md) — what `as` actually scopes.
- [Blocks](Blocks.md) — `@component` full-override interacts with imports.
- [Style DSL](Style%20DSL.md) — the index.
- [Parsing Style](../Markup/Phases/Parsing%20Style.md) — where the markup pipeline supplies the file loader.
- [Asset Management](../Engine/Asset%20Management.md) — what the standard file loader resolves through.
