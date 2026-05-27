# Blocks

The four top-level construct types: `animation`, `component`, `class`, `id`. These are what `ParseSheet` dispatches on, and what the [[Style DSL#Big picture|`StyleSheetHolder`]] stores.

> Source: `Sundex/Sundex.Style.DSL/StyleParser.cs` (`ParseBlock`), `Sundex/Sundex.Style.DSL/StyleSheetHolder.cs`, `Sundex/Sundex.Style.DSL/StyleSheet.cs`.

## The four kinds + the override prefix

```css
animation fade-in    { ... }      // → StyleSheetHolder.Animations["fade-in"]
component button     { ... }      // → StyleSheetHolder.Components["button"]
class    primary     { ... }      // → StyleSheetHolder.Classes["primary"]
id       save-btn    { ... }      // → StyleSheetHolder.IDTags["save-btn"]
@component button    { ... }      // → StyleSheetHolder.Components["button"], full replace
```

## `ParseBlock` — the shared engine

```csharp
private void ParseBlock(Dictionary<string, Dictionary<string, IStyleValue>> target,
    bool allowState = false, bool isOverride = false, StyleSheetHolder? sheet = null)
{
    SkipWhitespaceAndComments();
    var name = ReadIdentifier();
    if (isOverride) sheet?.FullOverrides.Add(name);
    SkipWhitespaceAndComments();
    Consume('{');
    var properties = new Dictionary<string, IStyleValue>();
    while (!Check('}')) {
        SkipWhitespaceAndComments();
        if (Check('}')) break;

        if (allowState && Match("state")) {
            Consume('[');
            var stateName = ReadIdentifier();
            Consume(']');
            SkipWhitespaceAndComments();
            Consume('=');
            var stateValue = ParseValue();
            properties[$"state[{stateName}]"] = stateValue;
        } else {
            var key = ReadIdentifier();
            SkipWhitespaceAndComments();
            Consume('=');
            var value = ParseValue();
            properties[key] = value;
        }

        SkipWhitespaceAndComments();
        if (Check(';')) Advance();
        SkipWhitespaceAndComments();
    }
    Consume('}');

    if (isOverride)
        target[name] = properties;                           // full replace
    else if (target.TryGetValue(name, out var existingProperties))
        foreach (var kvp in properties)
            existingProperties[kvp.Key] = kvp.Value;          // merge into existing
    else
        target[name] = properties;
}
```

Three knobs:

- `target` — which dictionary to populate (`Animations`, `Components`, `Classes`, or `IDTags`).
- `allowState` — whether `state[name] = ...` syntax is allowed inside.
- `isOverride` — whether to fully replace an existing entry.

## `animation`

```csharp
if (Match("animation"))
    ParseBlock(sheet.Animations, false, false, sheet);
```

`allowState = false` — animations don't have hover/pressed states. They have keyframes instead.

The properties inside an `animation` block are runtime-meaningful: `keyframes`, `timing-function`, `duration`. Nothing else is processed, but the parser doesn't reject other keys. They sit in the dictionary unused.

```css
animation fade-in {
    timing-function = "ease-in-out";
    duration = 1s;
    keyframes = !keyframes [
        0%   = { opacity = 0 },
        100% = { opacity = 1; loop = "reset" }
    ];
}
```

Becomes:

```
StyleSheetHolder.Animations["fade-in"] = {
    "timing-function": StringValue("ease-in-out"),
    "duration":        NumberValue(1, "s"),
    "keyframes":       KeyframesValue(...)
}
```

The `KeyframedAnimation` runtime objects come from `StyleSheet`'s constructor parsing this dictionary. See [[Animations|Animations]].

## `component`

```csharp
else if (Match("component"))
    ParseBlock(sheet.Components, true, false, sheet);
```

`allowState = true` — components support `state[hovered]` / `state[pressed]` blocks.

A `component` block sets defaults for every markup element with a matching tag name. The `name` is matched against `UIElement.Tag` — the markup parser's `node.TagName`.

```css
component button {
    padding = 5px;
    background = "#1c1c1c";
    font-size = 14px;
    border-radius = 5px;

    state[hovered] = { background = "#2a2a2a" }
    state[pressed] = { background = "#0f0f0f" }
}
```

Becomes:

```
StyleSheetHolder.Components["button"] = {
    "padding":          NumberValue(5, "px"),
    "background":       ColorValue("#1c1c1c"),
    "font-size":        NumberValue(14, "px"),
    "border-radius":    NumberValue(5, "px"),
    "state[hovered]":   BlockValue({...}),
    "state[pressed]":   BlockValue({...})
}
```

Note: the state key is `"state[hovered]"` as a string — the parser literally formats it that way. The lookup at runtime uses the same string format.

### Property naming convention

Properties use **kebab-case** (`font-size`, `border-radius`, `font-color`). This matches the names registered via `[NamedSetting("font-size")]` on `UIElement` properties. See [[../Components/Abstractions#The [NamedSetting] attribute|the [NamedSetting] attribute]].

If you misspell a property — `font_size` or `fontsize` — it will be silently ignored. The reflective application looks up properties by exact name match; unknown names are skipped.

### Component name = tag name

The component block name is the same string that appears as a markup tag:

| Component block | Matches markup |
|---|---|
| `component label { ... }` | `<label/>` |
| `component button { ... }` | `<button/>` |
| `component panel { ... }` | `<panel/>` |
| `component flex { ... }` | `<flex/>` |
| `component stack { ... }` | `<stack/>` |
| `component progress { ... }` | `<progress/>` |
| `component my-custom { ... }` | `<my-custom/>` (custom factory tag) |

There's nothing magical about which names are "real" — if a custom factory registers a `<waveform/>` tag, `component waveform { ... }` styles it.

## `class`

```csharp
else if (Match("class"))
    ParseBlock(sheet.Classes, true, false, sheet);
```

`allowState = true` — classes support state blocks too.

Matched against `UIElement.Classes` (parsed from `class="['primary', 'rounded']"` in markup, or `class="primary"` for a single class).

```css
class primary {
    background = "#3b82f6";
    font-color = "#ffffff";
}
```

Multiple classes can match; the first-found-wins logic is in `GetStyleValueForTag`. See **Specificity** below.

## `id`

```csharp
else if (Match("id"))
    ParseBlock(sheet.IDTags, true, false, sheet);
```

`allowState = true`.

Matched against `UIElement.ID` (set from `id="..."` in markup). Each ID should be unique within a markup document; no enforcement, but [[../Markup/Phases/Parsing Markup#ID and class registration|the markup phase]] uses last-write-wins.

```css
id save-btn {
    border-radius = 999px;
    background = "#22c55e";
}
```

## `@component` — full override

```csharp
else if (Peek() == '@') {
    Advance();
    if (Match("component")) ParseBlock(sheet.Components, true, true, sheet);
    else throw CreateException($"Unexpected token @ at {_pos}");
}
```

Currently only `@component` is supported (no `@class` or `@id`).

The `isOverride = true` flag does two things:

1. Adds the name to `StyleSheetHolder.FullOverrides`.
2. **Replaces** the existing entry instead of merging.

```css
// imported from default.snx.ss:
component button {
    background = "#1c1c1c";
    font-size = 14px;
    padding = 5px;
}

// in current file:
@component button {
    background = "#ff0000";
    color = "#ffffff";
    padding = 20px;
}
```

After parsing, `Components["button"]` is just:

```
{ "background": "#ff0000", "color": "#ffffff", "padding": 20px }
```

The original `font-size = 14px` is **gone**. With a non-`@`-prefixed redefinition:

```css
component button {
    background = "#ff0000";
    color = "#ffffff";
    padding = 20px;
}
```

You'd get the merge:

```
{ "background": "#ff0000", "color": "#ffffff",
  "font-size": 14px, "padding": 20px }
```

The `font-size = 14px` is preserved.

### Why the distinction?

Two reasons to want full replace:

- **Conflicting semantics**: a `padding = 20px` value might be incompatible with a previously-set `font-size` if the new component has a different layout intent.
- **Clean slate**: the imported definition has cruft you don't want carried forward.

`FullOverrides` is just a `HashSet<string>` of names — the runtime doesn't currently use it for anything (it's metadata). It's preserved through merging in `StyleSheetHolder.Merge`. Future tooling could use it (e.g. linters that warn "you're inheriting 7 properties from `default.snx.ss`'s button — did you mean to?").

## `state[name] = { ... }`

Inside `component`, `class`, or `id` blocks:

```css
state[hovered] = { background = "#2a2a2a" }
state[pressed] = { background = "#0f0f0f"; border-radius = 8px; }
```

State values are plain `BlockValue` — curly braces with one or more `key = value` pairs. No `!override` prefix. The stored key is the literal string `"state[hovered]"`.

State names are free-form strings. The current renderer recognises:

| State | When applied | Reset by |
|---|---|---|
| `hovered` | Cursor inside element bounds | Cursor leaves |
| `pressed` | Mouse button down on element | Button release or leave |

See [[../Components/Abstractions#UIState|UIState]] for the enum. New states can be added without changes to the parser.

### How states are looked up

```csharp
public Dictionary<string, IStyleValue>? GetStateOverrideForTag(string name, string state) {
    var key = $"state[{state}]";
    ...
    if (ids.TryGetValue(name, out var idProps) && idProps.TryGetValue(key, out var idState) &&
        idState is BlockValue idBlock) return idBlock.Properties;
    ...
}
```

The parser stores states as `"state[hovered]"` literal keys. The lookup builds the same key from the requested state name. The value must be a plain `BlockValue` — the `is BlockValue` check is what the lookup uses. This is why state blocks must be written as `{ ... }` without any `!override` prefix.

## Specificity

The lookup order is **id → class → component**:

```csharp
public IStyleValue? GetStyleValueForTag(string name, string property) {
    var ids = IDTags.GetAlternateLookup<ReadOnlySpan<char>>();
    var classes = Classes.GetAlternateLookup<ReadOnlySpan<char>>();
    var components = Components.GetAlternateLookup<ReadOnlySpan<char>>();

    if (ids.TryGetValue(name, out var idProps) && idProps.TryGetValue(property, out var idValue)) return idValue;
    if (classes.TryGetValue(name, out var classProps) && classProps.TryGetValue(property, out var classValue))
        return classValue;
    if (components.TryGetValue(name, out var componentProps) &&
        componentProps.TryGetValue(property, out var componentValue)) return componentValue;
    return null;
}
```

For each property request:

1. Look in `IDTags` — first match wins.
2. Look in `Classes`.
3. Look in `Components`.

**The lookup is by name**, not by selector specificity. The caller passes one name at a time:

```csharp
// in UIElement.ApplyStyleSheet (paraphrased):
foreach (var name in [Tag, ID, ...Classes])
    if (styleSheet.GetStyleValueForTag(name, propertyName) is { } value)
        ApplyStyleValue(...);
```

This means that if **`button#save-btn` has no specific `font-size` rule** but a class `primary` it has does, the class wins — even though id is "more specific" in the conventional sense. Specificity is **per-name, not per-property**.

There's no descendant combinator (`button label { ... }` style) — flat names only. This is intentional: layout-relative selectors complicate the matching engine and add cost. Most use cases are covered by ids and classes.

### `GetAlternateLookup<ReadOnlySpan<char>>`

The .NET 10 lookup variant lets the matching engine look up dictionary entries by `ReadOnlySpan<char>` instead of allocating a `string` per query. Important because `ApplyStyleSheet` may be called many times during state changes and during initial tree application.

## Property merging — same-name redefinition within a file

If `component button { ... }` appears twice in the same file:

```css
component button {
    background = "#000000";
    font-size = 14px;
}

component button {
    background = "#ffffff";
    border-radius = 5px;
}
```

`ParseBlock` merges with **later wins**:

```
Components["button"] = {
    "background":    ColorValue("#ffffff"),    // last wins
    "font-size":     NumberValue(14, "px"),    // preserved
    "border-radius": NumberValue(5, "px")      // added
}
```

Use `@component button { ... }` to **fully replace** instead.

## Threading

`ParseBlock` and the dispatch in `ParseSheet` are pure CPU. The whole parser is safe to run off-thread.

The `fileLoader` callback (used only by `ParseImport`) may interact with the filesystem or the asset cache — see [[Import|Import]] for that side.

## Related

- [[Syntax|Syntax]] — the lexical level (identifiers, numbers, strings).
- [[Style Types|Style Types]] — what `ParseValue` produces inside a property.
- [[Animations|Animations]] — what `animation` blocks resolve to at runtime.
- [[Import|Import]] — how `import "..."` interacts with `@component`.
- [[Style DSL|Style DSL]] — the index page.
- [[../Components/Abstractions#Style application — the [NamedSetting] flow|UIElement style application]] — how matched values get applied.
