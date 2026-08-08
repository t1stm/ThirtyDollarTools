# Style Types

Every concrete `IStyleValue` implementation produced by `ParseValue` and what each one stores. Nine value types in total, plus the `IStyleValue` interface itself.

> Source: `Sundex/Sundex.Style.DSL/Abstract/IStyleValue.cs`, `Sundex/Sundex.Style.DSL/Abstract/Values/`.

## `IStyleValue` — the base interface

```csharp
public interface IStyleValue
{
    public object Value { get; }
    public string ToString();
}
```

Two members:

- `Value` — the underlying data, boxed to `object`. Used when the consumer needs to inspect the raw payload without pattern-matching.
- `ToString()` — for debugging. Each implementation overrides it to produce stylesheet-shaped output (`"#ff0000"`, `14px`, `vec2(...)`).

All concrete implementations are **records** — value-equality, immutable, with positional constructors. This makes them cheap to compare and safe to share across `Dictionary<string, IStyleValue>` instances built by `StyleSheetHolder.Merge`.

## The full table

| Type | Underlying storage | Produced by parser when... |
|---|---|---|
| `NumberValue` | `float Value`, `string Unit` | `14`, `0.5`, `90deg`, `100%`, `1s` |
| `StringValue` | `string Value` | `"hello"`, or bare identifier `horizontal` |
| `ColorValue` | `string Value`, `Vector4 Vector` | `#ff0000`, `#ff0000ff`, `"#ff0000"` |
| `VectorValue` | `double X, Y, Z?, W?` | `vec2(...)`, `vec3(...)`, `vec4(...)` |
| `BlockValue` | `Dictionary<string, IStyleValue>` | `{ key = value; }` |
| `ArrayValue` | `List<IStyleValue>` | `[ a, b, c ]` |
| `MapValue` | `Dictionary<IStyleValue, IStyleValue>` | `[ key = val, key2 = val2 ]` |
| `KeywordValue` | `string Name` | `!unknown-keyword` (fallback) |
| `OverrideValue` | `IStyleValue` (wraps `BlockValue`) | `!override { ... }` |
| `GradientValue` | `IStyleValue` (wraps `BlockValue`) | `!gradient { ... }` |
| `KeyframesValue` | `IStyleValue` (wraps array/map) | `!keyframes [ ... ]` |
| `StopsValue` | `IStyleValue` (wraps array/map) | `!stops [ ... ]` |
| `DirectionValue` | `IStyleValue` | `!direction <value>` |

## Plain values

### `NumberValue`

```csharp
public record NumberValue(float Value, string Unit) : IStyleValue {
    object IStyleValue.Value => Value;
    public override string ToString() => $"{Value}{Unit}";
}
```

A float and an arbitrary unit string. The parser doesn't validate the unit — different consumers care about different units:

| Consumer | Acceptable units |
|---|---|
| `font-size` setter | `px`, `""` (treated as px) |
| `border-radius` setter | `px`, `""` |
| Animation `duration` | `ms`, `s`, `m` (others throw) |
| Vector dimension | usually `px` or `""` |
| Gradient stop percentage | `%`, `""` |

### `StringValue`

```csharp
public record StringValue(string Value) : IStyleValue {
    object IStyleValue.Value => Value;
    public override string ToString() => $"\"{Value}\"";
}
```

Wraps a bare string. Either from `"..."` literal or from a bare identifier like `horizontal`, `vertical`, `center`, `linear`.

The bare-identifier fallback is what makes `direction = horizontal;` work — the parser falls through to `new StringValue("horizontal")`. Quotes are optional unless the value contains spaces or special characters.

### `ColorValue`

```csharp
public record ColorValue(string Value) : IStyleValue {
    public Vector4 Vector { get; } = ParseColorFromHex(Value);
    object IStyleValue.Value => Value;

    public override string ToString() => Value;

    internal static Vector4 ParseColorFromHex(ReadOnlySpan<char> hex) {
        var hexTrimmed = hex.TrimStart('#');
        if (hexTrimmed.Length is not (6 or 8))
            throw new ArgumentException("Invalid hex color format, expected #RRGGBB(AA)");
        ...
        return new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f);
    }
}
```

Stores the original hex string **and** the parsed `Vector4`. The vector is computed eagerly in the record initialiser, so:

- Invalid hex throws **at parse time** — not at render time.
- The vector is shared across all callers — no re-parsing per access.

The `Vector4` is in linear `[0, 1]` space (RGBA), which is what OpenGL uniforms expect. No gamma correction.

#### Hex shorthand

3-digit (`#fff`) and 4-digit (`#fffa`) shorthands are **not supported**. The validation is strict: 6 or 8 hex digits only.

#### Two ways to write a color

```css
background = "#ff0000";       // string with leading #, reinterpreted as ColorValue
background = #ff0000;          // unquoted, parsed as ColorValue directly
```

Both produce identical `ColorValue("#ff0000")`. The unquoted form is preferred — it's shorter and visually distinct.

### `VectorValue`

```csharp
public record VectorValue(double X, double Y, double? Z = null, double? W = null) : IStyleValue
{
    public VectorValue(ReadOnlySpan<NumberValue> values) : this(0, 0) {
        switch (values.Length) {
            case < 2: throw new ArgumentException("Vector must have at least 2 values.");
            case 2: (X, Y) = (values[0].Value, values[1].Value); break;
            case 3: (X, Y, Z) = (values[0].Value, values[1].Value, values[2].Value); break;
            case 4: (X, Y, Z, W) = (values[0].Value, values[1].Value, values[2].Value, values[3].Value); break;
            default: throw new ArgumentException("Vector must have at most 4 values.");
        }
    }

    public int Count { get; } = 2 + (Z != null ? 1 : 0) + (W != null ? 1 : 0);
    object IStyleValue.Value => (X, Y, Z, W);
    public override string ToString() => $"vec{Count}(...)";
}
```

A 2-, 3-, or 4-component vector. The `Count` property tells consumers how many dimensions are populated.

Used for transforms (`vec2(10px, -10px)`), scales (`vec2(1.5, 1.5)`), arbitrary positional data. Animation properties consume it — see [Animations](Animations.md#property-mapping).

The doubles vs. floats: `VectorValue` stores `double` internally for precision, but the source `NumberValue.Value` is `float`. The cast happens in the constructor.

### Why span-based construction?

```csharp
private VectorValue ParseVector(int dimensions) {
    var values = ArrayPool<NumberValue>.Shared.Rent(dimensions);
    var span = values.AsSpan()[..dimensions];
    ...
    ArrayPool<NumberValue>.Shared.Return(values);
    return new VectorValue(span);
}
```

The parser uses `ArrayPool<NumberValue>` to avoid an allocation per `vec2(...)` call. The `VectorValue(ReadOnlySpan<NumberValue>)` constructor copies the values out before the array is returned to the pool. Cheap and safe.

## Composite values

### `BlockValue`

```csharp
public record BlockValue(Dictionary<string, IStyleValue> Properties) : IStyleValue {
    object IStyleValue.Value => Properties;
    public override string ToString() => "{ ... }";
}
```

A nested key-value block:

```css
background = !gradient {
    type = "linear";
    direction = 90deg;
    stops = !stops [...];
};
```

The outer `!gradient` is a `GradientValue` wrapping a `BlockValue` containing `type`, `direction`, `stops`. Nested blocks support both `;` and `,` as property separators:

```css
gradient = !gradient {
    type = "linear",
    direction = 90deg,    // commas work
    stops = !stops [...]
};
```

### `ArrayValue`

```csharp
public record ArrayValue(List<IStyleValue> Values) : IStyleValue {
    object IStyleValue.Value => Values;
    public override string ToString() => "[ ... ]";
}
```

A comma-separated list. Polymorphic — array elements can be any `IStyleValue` type.

```css
stops = !stops [
    "#ff0000",
    "#00ff00",
    "#0000ff"
];
```

Becomes `StopsValue` wrapping `ArrayValue` with three `ColorValue` elements.

### `MapValue`

```csharp
public record MapValue(Dictionary<IStyleValue, IStyleValue> Values) : IStyleValue {
    object IStyleValue.Value => Values;
    public override string ToString() => "[ key=value, ... ]";
}
```

The same `[...]` brackets, but with `=` between elements. Triggered when the parser sees `key = value` instead of just `value`:

```csharp
private IStyleValue ParseArrayOrMap() {
    Consume('[');
    var list = new List<IStyleValue>();
    var map = new Dictionary<IStyleValue, IStyleValue>();
    var isMap = false;

    while (!Check(']')) {
        ...
        var val = ParseValue();
        SkipWhitespaceAndComments();
        if (Check('=')) {
            isMap = true;
            Advance();
            var val2 = ParseValue();
            map[val] = val2;
        } else {
            list.Add(val);
        }
        ...
    }

    return isMap ? new MapValue(map) : new ArrayValue(list);
}
```

The decision is made when the **first `=`** is seen. If you mix:

```css
[ a, b = c, d ]      // first val 'a' goes into list, then 'b = c' triggers isMap=true
```

The `a` is lost — the parser had already added it to `list`, but the result returned is `MapValue(map)`. Don't mix.

The map keys can be any value type but typically are `NumberValue` percentages or `ColorValue`s:

```css
stops = !stops [
    0%   = "#ff0000",
    50%  = "#ffff00",
    100% = "#00ff00"
];
```

Keys are `NumberValue(0, "%")`, `NumberValue(50, "%")`, etc.

## Keyword values

### `KeywordValue` — the fallback

```csharp
public record KeywordValue(string Name) : IStyleValue {
    object IStyleValue.Value => Name;
    public override string ToString() => $"!{Name}";
}
```

Catches any `!name` not matched by the special-cased keywords. The dispatch:

```csharp
private IStyleValue ParseKeyword() {
    Consume('!');
    var name = ReadIdentifier();
    SkipWhitespaceAndComments();
    return name switch {
        "override"  => new OverrideValue(ParseValue()),
        "gradient"  => new GradientValue(ParseValue()),
        "keyframes" => new KeyframesValue(ParseValue()),
        "stops"     => new StopsValue(ParseValue()),
        "direction" => new DirectionValue(ParseValue()),
        _           => new KeywordValue(name)
    };
}
```

Unknown keywords (like `!my-custom`) become `KeywordValue("my-custom")` — the parser doesn't reject them. They're a no-op for consumers that don't recognise them. This is intentional: future extensions can add new keywords without breaking older parsers (forward compatibility).

### `OverrideValue`

```csharp
public record OverrideValue(IStyleValue Value) : IStyleValue {
    public BlockValue Properties => (BlockValue)Value;
    object IStyleValue.Value => Value;
    public override string ToString() => "!override " + Value;
}
```

Produced by `!override { ... }` in the stylesheet. Wraps a `BlockValue` and exposes it via the `Properties` accessor (which casts the inner value — writing `!override "string"` instead of a block will throw at access time).

`OverrideValue` is **not** used for state blocks. `GetStateOverrideForTag` checks `idState is BlockValue`, so it only matches plain `BlockValue` entries — an `OverrideValue` wrapper would fail that check silently. State blocks must be written without the `!override` prefix:

```css
// correct — GetStateOverrideForTag finds this:
state[hovered] = { background = "#2a2a2a" }

// wrong — the !override wrapper causes the lookup to return null:
state[hovered] = !override { background = "#2a2a2a" }
```

`OverrideValue` is parsed and stored in the `IStyleValue` AST when explicitly written, but no part of the current runtime lookup uses it.

### `GradientValue`

```csharp
public record GradientValue(IStyleValue Value) : IStyleValue
{
    public string? Type { get; } = (Value as BlockValue)?.Properties.TryGetValue("type", out var v) == true
        ? v.Value as string
        : null;

    public IStyleValue? Direction { get; } =
        (Value as BlockValue)?.Properties.TryGetValue("direction", out var v) == true
            ? v
            : null;

    public List<StopsValue.GradientStop> Stops { get; } =
        Value is not BlockValue block || !block.Properties.TryGetValue("stops", out var v)
            ? []
            : v switch {
                StopsValue sv => sv.Stops,
                MapValue or ArrayValue => StopsValue.BuildStops(v),
                _ => []
            };

    object IStyleValue.Value => Value;
    public override string ToString() => "!gradient " + Value;
}
```

The richest of the value types. Three pre-extracted properties from the inner block:

- `Type` — `"linear"` or `"radial"` (typically). Pulled from `block.Properties["type"]`.
- `Direction` — could be a `NumberValue` (degrees) or a `DirectionValue` (e.g. `!direction outward`).
- `Stops` — the gradient color stops, normalised to a `List<GradientStop>`.

The `Stops` extractor accepts three input shapes:

| Form | Example |
|---|---|
| `StopsValue` | `stops = !stops [0% = "#ff0000", 100% = "#00ff00"]` |
| `MapValue` | `stops = [0% = "#ff0000", 100% = "#00ff00"]` |
| `ArrayValue` | `stops = ["#ff0000", "#00ff00"]` |

The `MapValue`/`ArrayValue` cases are a fallback — `StopsValue.BuildStops` handles the same conversion as if they were prefixed with `!stops`. This means `!stops` is **optional** in practice for the `stops` property of a gradient.

The `GenerateGradientPlane` method (defined on `GradientValue`, called from [`Panel.ApplyStyleValue`](../Components/Panels.md#panel) and [`ProgressBar`](../Components/Bars.md#progressbar)) builds the actual `Renderable` from this metadata. Lives outside the parser — it's a runtime concern.

### `KeyframesValue`

```csharp
public record KeyframesValue(IStyleValue Value) : IStyleValue {
    public IStyleValue Elements => Value;
    public List<KeyframeStep> Keyframes => BuildKeyframes(Value);
    ...
    public record KeyframeStep(double Percentage, Dictionary<string, IStyleValue> Properties);
}
```

Wraps an array or map of keyframe steps. The `BuildKeyframes` static converts both forms into a uniform `List<KeyframeStep>`:

| Input form | Output |
|---|---|
| `MapValue` (`0% = {...}, 100% = {...}`) | One step per entry, percentage from key. |
| `ArrayValue` with explicit `%` properties | Steps with explicit percentages preserved. |
| `ArrayValue` (just blocks, no percentages) | Steps with **interpolated** percentages: first = 0%, last = 100%, middle = linearly distributed. |

The interpolation logic for arrays is non-trivial — see [Animations](Animations.md#the-interpolated-array-form) for the gory details.

### `StopsValue`

```csharp
public record StopsValue(IStyleValue Value) : IStyleValue
{
    public IStyleValue Elements => Value;
    public List<GradientStop> Stops => BuildStops(Value);
    ...
    public record GradientStop(ColorValue Color, float Percentage);
}
```

Sibling of `KeyframesValue` but for gradient color stops. Same dual array/map form:

| Input form | Output |
|---|---|
| `MapValue` (`0% = "#fff", 100% = "#000"`) | Stop with explicit percentages from keys. |
| `ArrayValue` (just colors) | Stops with **evenly distributed** percentages: first = 0, last = 1, middle = `i/(n-1)`. |

The result is **always sorted by percentage** ascending — `list.Sort((a, b) => a.Percentage.CompareTo(b.Percentage));`. This is what gradient renderers expect.

### `DirectionValue`

```csharp
public record DirectionValue(IStyleValue Value) : IStyleValue {
    object IStyleValue.Value => Value;
    public override string ToString() => "!direction " + Value;
}
```

A thin wrapper around any value. Used to tag a value as a direction:

```css
direction = !direction outward;          // DirectionValue(StringValue("outward"))
direction = !direction up;               // DirectionValue(StringValue("up"))
direction = 90deg;                       // NumberValue, no wrapper
```

The wrapper exists so that consumers can distinguish "this is a named direction" from "this is a number that happens to be in degrees." A radial gradient might accept `!direction outward` (radial outward) or `!direction inward`, where a number doesn't make sense.

For most cases (linear gradients), the angle in degrees is enough — no `!direction` needed.

## Threading

All `IStyleValue` records are immutable (read-only properties on records). Safe to share across threads. Multiple `UIElement`s can hold references to the same `ColorValue` etc. without synchronisation.

Note: `BlockValue.Properties` and `ArrayValue.Values` and `MapValue.Values` are **mutable collections**. Mutating them after parse would affect every element using them. Don't.

## Related

- [Syntax](Syntax.md) — what input shapes produce each type.
- [Blocks](Blocks.md) — top-level `animation` / `component` / `class` / `id` blocks (the dictionaries that contain these values).
- [Animations](Animations.md) — `KeyframesValue` and how `KeyframedAnimation` is built from it.
- [Panel.ApplyStyleValue](../Components/Panels.md#panel) — where `ColorValue`/`GradientValue` actually become a background.
- [Style DSL](Style%20DSL.md) — the index page.
