# Style DSL

Sundex's stylesheet language. Lives in `Sundex.Style.DSL`. Files end in `.snx.ss`. The role is the same as CSS: keep visual presentation out of the markup tree and let one file restyle many components. The syntax is **not** CSS — it's a small purpose-built DSL with explicit value types (numbers, vectors, colors, blocks, arrays/maps) and a set of `!keyword` directives for things like gradients and animations.

> Source: `Sundex/Sundex.Style.DSL/`.

## What lives here

- **[[Syntax|Syntax]]** — characters, identifiers, numbers, strings, hex colors, comments. The lexical layer.
- **[[Blocks|Blocks]]** — the four top-level block types (`animation`, `component`, `class`, `id`), their semantics and the `@component` full-override form.
- **[[Style Types|Style Types]]** — every `IStyleValue` implementation and what the parser produces for each kind of input.
- **[[Animations|Animations]]** — `!keyframes`, `timing-function`, `duration`, `loop`, and the `KeyframedAnimation` it produces.
- **[[Import|Import]]** — the `import "..."` directive, file-loader callback, and cycle protection.

## Big picture

```
.snx.ss text
    │
    │  StyleParser.Parse(dsl, fileLoader)
    ▼
StyleSheetHolder            ← raw AST: dictionaries keyed by name
    │  Animations  (Dictionary<string, Dictionary<string, IStyleValue>>)
    │  Components (              "                          )
    │  Classes    (              "                          )
    │  IDTags     (              "                          )
    │  FullOverrides (HashSet<string>)
    │
    │  new StyleSheet(holder)
    ▼
StyleSheet                  ← matching engine
    │  ComputedAnimations (Dictionary<string, KeyframedAnimation>)
    │  Components / Classes / IDTags  (the holder's dictionaries, untouched)
    │
    │  GetStyleValueForTag(name, prop)        ← id > class > component
    │  GetStateOverrideForTag(name, state)    ← id > class > component
    ▼
UIElement.ApplyStyleSheet(this)
    │  reflects [NamedSetting] properties → calls ApplyStyleValue
    ▼
UI tree visually styled
```

The `StyleSheetHolder` is the parser output (raw AST). The `StyleSheet` adds:

1. **Pre-computed animations** (`ComputedAnimations`) — `KeyframesValue` blocks are converted into runtime `KeyframedAnimation` objects with `Keyframe` lists and stepping functions.
2. **Lookup methods** (`GetStyleValueForTag`, `GetStateOverrideForTag`) — given a tag/id/class name and a property, return the matching value with **id > class > component** specificity.

## File extension

`.snx.ss` — "Sundex Style Sheet." Two-segment extension to coexist with `.snx.smxl` (markup) without ambiguity.

The convention is a project-level choice; `StyleParser.Parse` doesn't care about the extension. As long as the `string dsl` argument contains valid DSL, it parses.

## Top-level structure

A stylesheet is a flat list of these constructs:

```css
import "path/to/other.snx.ss";       // — text-include from another file

animation fade-in { ... }            // — named animation block
component button { ... }             // — defaults for a markup tag
class primary { ... }                // — defaults for elements with class="primary"
id main-button { ... }               // — defaults for the element with id="main-button"
@component button { ... }            // — full override (replaces inherited defaults)
```

Order is significant for **redefinitions within the same file** (later wins) but not for the difference between specificity tiers (id always beats class always beats component, regardless of file order).

See [[Blocks|Blocks]] for what's legal inside each kind of block.

## Where it plugs in

The Style DSL is consumed by the [[../Markup/Phases/Parsing Style|Parsing Style]] phase of the markup pipeline. The pipeline:

1. Reads `<style src="...">` or `<style>...</style>` from the [[../Markup/Markup Parser|markup document]].
2. Calls `StyleParser.Parse(source, importResolver)` to get a `StyleSheetHolder`.
3. Wraps the holder in `new StyleSheet(holder)`.
4. After [[../Markup/Phases/Parsing Markup|building the UI tree]], calls `uiElement.ApplyStyleSheet(styleSheet)`.

`ApplyStyleSheet` walks the realised tree and matches each element's `ID`, `Classes`, and `Tag` against the stylesheet via `GetStyleValueForTag`. Matched values are written into `[NamedSetting]`-tagged properties — see [[../Components/Abstractions#Style application — the [NamedSetting] flow|UIElement style application]].

## Quick example

```css
// Animations
animation pulse {
    duration = 1s;
    timing-function = "ease-in-out";
    keyframes = !keyframes [
        0%   = { opacity = 1 },
        50%  = { opacity = 0.5; loop = "invert" },
        100% = { opacity = 1 }
    ];
}

// Component defaults — everything tagged <button> picks these up
component button {
    background     = "#1c1c1c";
    font-color     = "#ffffff";
    font-size      = 14px;
    border-radius  = 5px;
    padding        = 5px;

    state[hovered] = !override { background = "#2a2a2a"; };
    state[pressed] = !override { background = "#0f0f0f"; border-radius = 8px; };
}

// Class — opt-in via class="primary"
class primary {
    background = !gradient {
        type = "linear";
        direction = 90deg;
        stops = !stops [
            0%   = "#3b82f6",
            100% = "#1d4ed8"
        ];
    };
}

// ID — applied to <... id="save-btn"/>
id save-btn {
    border-radius = 999px;     // stadium shape
}
```

The corresponding markup:

```xml
<button id="save-btn" class="primary">
    <label value="Save"/>
</button>
```

resolves the **background** as id (no override) → class (`!gradient ...`) → component (`#1c1c1c`). The class wins. The **border-radius** resolves id (`999px`) → class (no override) → component (`5px`). The id wins.

## Threading

`StyleParser.Parse` is pure CPU + filesystem — safe to call off the GL thread. The `fileLoader` callback may queue GL operations indirectly via `AssetProvider.Load`, but parse itself doesn't.

`StyleSheet` construction is also pure CPU — no GL.

Applying the stylesheet (`uiElement.ApplyStyleSheet`) **is** GL-only because creating gradient planes and color planes for backgrounds allocates GPU resources.

See [[../Engine/Threading|Threading]] for the broader picture.

## Related

- [[Syntax|Syntax]] — the lexical level.
- [[Blocks|Blocks]] — the structural level.
- [[Style Types|Style Types]] — every `IStyleValue` produced by the parser.
- [[Animations|Animations]] — `!keyframes` and the runtime animation system.
- [[Import|Import]] — `import "..."` directives.
- [[../Markup/Phases/Parsing Style|Parsing Style]] — the markup phase that consumes a `.snx.ss` file.
- [[../Components/Abstractions#Style application — the [NamedSetting] flow|UIElement.ApplyStyleSheet]] — what happens after a match.
