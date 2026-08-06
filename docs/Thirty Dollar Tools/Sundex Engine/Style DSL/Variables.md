# Variables

Named constants in a stylesheet. Declared with `var`, used with `$`. Their whole purpose is
to stop a palette from being copy-pasted into thirty property values.

> Source: `Sundex/Sundex.Style.DSL/StyleParser.cs` (`ParseVariable`, `ParseVariableReference`),
> `Sundex/Sundex.Style.DSL/StyleSheetHolder.cs` (`Variables`, `Namespaces`).
> Example: `Sundex/Sundex.Style.DSL/Examples/variables.snx.ss`.

## Syntax

```css
var text_color   = "#ffffffff";
var padding_lg   = 20px;
var card_shadow  = { blur = 4px; color = "#00000088"; };

class card {
    font-color = $text_color;
    padding    = $padding_lg;
    shadow     = $card_shadow;
}
```

A `var` declaration is a **top-level** statement, alongside `class`, `component`, `id`,
`animation` and `import`. It can't be written inside a block. The trailing `;` is optional,
as everywhere else.

The right-hand side is a full `ParseValue` call, so a variable can hold **any** value the DSL
has: numbers with units, colors, strings, vectors, blocks, arrays, maps, `!keyword` values.

Because `$` is handled inside `ParseValue`, a reference works anywhere a value works —
nested blocks, array elements, map keys and values, vector components, keyframe properties:

```css
var accent = "#ff0000ff";
var offset = 4;

class card {
    transform = vec2($offset, $offset);
    shadow    = [ $accent, $accent ];
    state[hovered] = !override { border-color = $accent; };
}
```

## Constants, resolved at parse time

`$x` is **substituted** the moment it's parsed — the parser looks the name up and returns the
stored `IStyleValue`. Nothing about variables survives into `StyleSheet` or reaches
`UIElement.ApplyStyleSheet`; by the time a stylesheet is applied, every reference has already
become a plain value.

Two consequences:

1. **Declare before use.** The parser is a single forward pass, so a `$x` that appears above
   its `var x` throws `Unknown variable 'x'`. Same rule as [[Import|imports]] — order matters.
2. **No mutation.** There is no way to reassign a variable. A future `var mut` could add it;
   nothing here is in the way.

The substituted `IStyleValue` is shared by reference across every use site. All value types are
records and nothing mutates them after a parse, so this is safe — but it's the reason values
must stay treated as immutable.

## Redefinition and shadowing

Declaring the same name twice **in one file** throws:

```css
var accent = "#ff0000ff";
var accent = "#00ff00ff";   // Variable 'accent' is already defined in this file
```

Redefining a name that arrived from an import is **legal**, and is how you override one value
of an imported palette:

```css
import "theme.snx.ss";      // declares accent
var accent = "#00ff00ff";   // fine — shadows theme's value for the rest of this file
```

The parser tracks declarations of the current file in a per-parser `HashSet`, and each imported
file gets its own parser — so "same file" falls out of the structure for free.

## Scoping across imports

Variables are the **only** thing in the DSL with any scoping. Classes, ids, components and
animations are always global and merge exactly as [[Import|Import]] describes, aliased or not.

### Plain import — variables merge globally

```css
// theme.snx.ss
var accent = "#ff0000ff";

// main.snx.ss
import "theme.snx.ss";
class card { border-color = $accent; }   // OK
```

The imported file's variables land in the importer's global variable scope. Collisions follow
the same last-write-wins rule as block properties.

### Named import — variables behind an alias

```css
import "theme.snx.ss" as theme;

class card {
    border-color = $theme.accent;   // OK
    background   = $accent;         // Unknown variable 'accent'
}
```

`import "..." as name;` puts that file's variables into a namespace instead of the global scope.
Its `class` / `id` / `component` / `animation` blocks still merge globally — the alias affects
variables and nothing else.

An unknown alias throws `Unknown import alias 'x'`; a known alias with an unknown member throws
`Import 'x' has no variable 'y'`.

### Aliases are file-local

An alias belongs to the file that declared it. It is **not** re-exported:

```css
// middle.snx.ss
import "theme.snx.ss" as theme;

// main.snx.ss
import "middle.snx.ss";
class card { color = $theme.accent; }   // Unknown import alias 'theme'
```

Variables propagate through a chain of plain imports; aliases don't propagate at all.

### The same file, both ways

```css
import "theme.snx.ss";
import "theme.snx.ss" as theme;

class card {
    color  = $accent;         // OK
    border = $theme.accent;   // OK, same value
}
```

Both work, in either order. See [[Import#The parse cache|Import]] for why this needed the parse
cache — the older `_importedPaths` set would have skipped the second directive entirely and left
the alias empty.

## Storage

```csharp
public Dictionary<string, IStyleValue> Variables { get; } = new();
public Dictionary<string, Dictionary<string, IStyleValue>> Namespaces { get; } = new();
```

`Merge(other, includeVariables: true)` copies `other.Variables` over `Variables` and **never**
touches `Namespaces` — that's what makes aliases file-local. An aliased import is the one caller
that passes `includeVariables: false`.

Both dictionaries live on `StyleSheetHolder` only. `StyleSheet` doesn't expose them; substitution
is done by the time it's constructed, so nothing downstream needs them.

## Limits

- No reassignment (`var mut` is a possible future, unimplemented).
- No string interpolation — `"prefix-$x"` is a literal string containing `$x`.
- No arithmetic — `$padding + 4px` doesn't parse.
- `$x` can't be a property key, block name, or import path.
- No block-local variables; declaration is top-level only.

## Related

- [[Import|Import]] — the `as` form, the parse cache, and merge ordering.
- [[Syntax|Syntax]] — identifier characters (`_` included) and the `ParseValue` dispatch.
- [[Style Types|Style Types]] — what a variable can hold.
- [[Style DSL|Style DSL]] — the index.
