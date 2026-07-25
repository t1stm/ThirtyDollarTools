# Animations

The `animation` block in a `.snx.ss` file becomes a `KeyframedAnimation` at runtime. The conversion happens once, in `StyleSheet`'s constructor, when an existing `StyleSheetHolder` is wrapped.

> Source: `Sundex/Sundex.Style.DSL/StyleSheet.cs` (`ParseAnimations`, `ParseKeyframeProperties`), `Sundex/Sundex.Style.DSL/Abstract/Values/Keywords/KeyframesValue.cs`, `Sundex.Core/Animations/` (the `Keyframe`/`KeyframedAnimation`/`SteppingFunction` types).

## The shape

```css
animation fade-in {
    timing-function = "ease-in-out";    // optional, defaults to "linear"
    duration        = 1s;                // required (>= 1ms)
    keyframes       = !keyframes [...];  // required
}
```

Three properties:

| Property | Type | Required | Notes |
|---|---|---|---|
| `timing-function` | `StringValue` | No | Default `"linear"`. Parsed via `SteppingFunctions.ParseSteppingFunction`. |
| `duration` | `NumberValue` | Yes (effectively) | Units: `ms`, `s`, `m`. Must be `>= 1ms`. |
| `keyframes` | `KeyframesValue` | Yes | Array or map of keyframe blocks. |

If `keyframes` is missing the animation is **silently skipped** — `ParseAnimations` does `if (keyframesValue is null) continue;`. A `duration` < 1 throws.

## `ParseAnimations` — the converter

```csharp
private static Dictionary<string, KeyframedAnimation> ParseAnimations(StyleSheetHolder holder) {
    var animations = new Dictionary<string, KeyframedAnimation>();

    foreach (var (animationName, values) in holder.Animations) {
        var keyframes = new List<Keyframe>();
        var keyframesValue = values.TryGetValue("keyframes", out var keyframesStyleValue)
            ? (KeyframesValue)keyframesStyleValue
            : null;

        if (keyframesValue is null) continue;

        var globalSteppingFunction = SteppingFunction.Linear;
        if (values.TryGetValue("timing-function", out var steppingFunctionValue) &&
            steppingFunctionValue is StringValue sv)
            globalSteppingFunction = SteppingFunctions.ParseSteppingFunction(sv.Value);

        var globalLength = 0;
        if (values.TryGetValue("duration", out var lengthValue) && lengthValue is NumberValue lv)
            globalLength = lv.Unit switch {
                "ms" => (int)lv.Value,
                "s"  => (int)(lv.Value * 1000),
                "m"  => (int)(lv.Value * 60000),
                _    => throw new ArgumentException($"Invalid length unit {lv.Unit}")
            };

        if (globalLength < 1) throw new ArgumentException("Keyframes length must be positive");

        var previousPercentage = 0.0;
        foreach (var (percentage, propertiesBlock) in keyframesValue.Keyframes) {
            var deltaPct = percentage - previousPercentage;
            if (deltaPct < 0) deltaPct = 0;
            previousPercentage = percentage;

            var keyframe = new Keyframe {
                SteppingFunction = globalSteppingFunction,
                LengthMs = (float)(globalLength * deltaPct)
            };

            foreach (var (property, value) in propertiesBlock)
                ParseKeyframeProperties(property, value, ref keyframe);
            keyframes.Add(keyframe);
        }

        var keyframed = new KeyframedAnimation(keyframes);
        animations.Add(animationName, keyframed);
    }
    return animations;
}
```

Three phases per animation:

1. **Resolve global properties** — `timing-function` and `duration`.
2. **Convert each keyframe** — for each step in `keyframesValue.Keyframes`, build a `Keyframe` with its `LengthMs` set proportional to `deltaPct * globalLength`.
3. **Construct `KeyframedAnimation`** — wrap the list, store under the animation name.

### Per-keyframe length, not absolute time

The `LengthMs` of each keyframe is the **time spent transitioning** from the previous keyframe to this one — not the absolute time-from-start. So a `[0%, 33%, 100%]` map with `duration = 1s` produces:

| Keyframe | Percentage | `LengthMs` |
|---|---|---|
| 0 | 0.00 | `0.00 - 0.00 = 0.00 * 1000 = 0` |
| 1 | 0.33 | `0.33 - 0.00 = 0.33 * 1000 = 330` |
| 2 | 1.00 | `1.00 - 0.33 = 0.67 * 1000 = 670` |

Total: 0 + 330 + 670 = 1000ms. Matches the `duration`.

### Out-of-order keyframes

```csharp
var deltaPct = percentage - previousPercentage;
if (deltaPct < 0) deltaPct = 0;
```

If keyframes are not monotonically increasing in percentage (e.g. `0%, 50%, 30%, 100%`), the negative delta is **clamped to zero**, not thrown. The resulting animation will skip backwards instantaneously. Probably not what you want — but it doesn't crash.

The keyframes from `MapValue` come back in **dictionary insertion order**, not sorted — so writing them out of order in the source produces out-of-order keyframes. Always write keyframes in ascending percentage.

## Time-unit conversion

```csharp
globalLength = lv.Unit switch {
    "ms" => (int)lv.Value,
    "s"  => (int)(lv.Value * 1000),
    "m"  => (int)(lv.Value * 60000),
    _    => throw new ArgumentException($"Invalid length unit {lv.Unit}")
};
```

Three accepted units. Anything else throws — including the empty unit `""`.

| Source | `globalLength` |
|---|---|
| `500ms` | 500 |
| `1s` | 1000 |
| `2.5s` | 2500 |
| `1m` | 60000 |
| `1` | **throws** ("Invalid length unit ") |
| `1minutes` | **throws** ("Invalid length unit minutes") |

The cast to `int` truncates fractional milliseconds — `1.5ms` becomes 1ms. Unlikely to matter in practice.

## Keyframe properties — `ParseKeyframeProperties`

```csharp
private static void ParseKeyframeProperties(string property, IStyleValue value, ref Keyframe keyframe)
{
    switch (property) {
        case "timing-function" when value is StringValue steppingFunctionString:
            keyframe.SteppingFunction =
                SteppingFunctions.ParseSteppingFunction(steppingFunctionString.Value);
            break;

        case "transform" when value is VectorValue vectorValue:
            keyframe.Position = vectorValue.Count switch {
                2 => new Vector3((float)vectorValue.X, (float)vectorValue.Y, 0),
                3 => new Vector3((float)vectorValue.X, (float)vectorValue.Y, (float)(vectorValue.Z ?? 0)),
                _ => throw new ArgumentException("Invalid vector count for transform property")
            };
            break;

        case "opacity" when value is NumberValue numberValue:
            keyframe.Opacity = numberValue.Unit is "%" ? numberValue.Value / 100f : numberValue.Value;
            break;

        case "color" when value is ColorValue colorValue:
            keyframe.Color = colorValue.Vector;
            break;

        case "loop" when value is StringValue loopString:
            keyframe.LoopingMode = loopString.Value switch {
                "none"        => AnimationLoopingMode.None,
                "invert"      => AnimationLoopingMode.Invert,
                "loop-start"  => AnimationLoopingMode.LoopStart,
                "reset"       => AnimationLoopingMode.ResetToStart,
                _             => throw new ArgumentException("Invalid loop mode")
            };
            break;

        case "scale" when value is VectorValue vectorValue:
            keyframe.Scale = vectorValue.Count switch {
                2 => new Vector3((float)vectorValue.X, (float)vectorValue.Y, 1),
                3 => new Vector3((float)vectorValue.X, (float)vectorValue.Y, (float)(vectorValue.Z ?? 1)),
                _ => throw new ArgumentException("Invalid scale vector length")
            };
            break;
    }
}
```

Six recognised properties per keyframe.

### Property mapping

| Property | DSL form | `Keyframe` field | Notes |
|---|---|---|---|
| `timing-function` | `"ease-in"` | `SteppingFunction` | Per-keyframe override of the animation-level setting. |
| `transform` | `vec2(10px, -10px)` or `vec3(...)` | `Position` (`Vector3`) | 2D vec gets `Z=0`. |
| `opacity` | `0.5` or `50%` | `Opacity` (`float`) | `%` divides by 100. |
| `color` | `#ff0000` | `Color` (`Vector4`) | The element's color tint, not background. |
| `scale` | `vec2(1.5, 1.5)` | `Scale` (`Vector3`) | 2D vec gets `Z=1` (identity scale). |
| `loop` | `"reset"`, `"invert"`, `"loop-start"`, `"none"` | `LoopingMode` | Set on the **last** keyframe. |

Anything not in this list (e.g. `font-size`, `border-radius`) — silently ignored. Animations only animate transform, opacity, color, scale.

### Why scale's Z-default differs from transform

```csharp
case "transform" when value is VectorValue vectorValue:
    keyframe.Position = vectorValue.Count switch {
        2 => new Vector3((float)vectorValue.X, (float)vectorValue.Y, 0),    // Z=0
        ...
    };

case "scale" when value is VectorValue vectorValue:
    keyframe.Scale = vectorValue.Count switch {
        2 => new Vector3((float)vectorValue.X, (float)vectorValue.Y, 1),    // Z=1
        ...
    };
```

`transform` is a positional offset — Z=0 means "no Z translation." `scale` is a multiplier — Z=1 means "identity along Z." The defaults are unit-of-the-operation, not zero-of-the-operation.

### `loop` modes

| Mode | Behavior |
|---|---|
| `none` | Stop at the end. |
| `invert` | Reverse direction; play backwards to start, then forwards again, indefinitely. |
| `loop-start` | At end, jump to the **first keyframe** position and continue. |
| `reset` | At end, jump to "starting state" (zero offset/identity) and continue. |

Set on the **last** keyframe — controls what happens when the animation completes one cycle.

## Keyframe form variants

`KeyframesValue.BuildKeyframes` accepts two forms — see [[Style Types#KeyframesValue|Style Types]] for the value-level breakdown. From the animation system's point of view, both produce the same `List<KeyframeStep>`.

### Map form — explicit percentages

```css
keyframes = !keyframes [
    0%   = { opacity = 0 },
    100% = { opacity = 1; loop = "reset" }
];
```

Each entry has its percentage as the key. Straightforward.

### Array form — implicit percentages

```css
keyframes = !keyframes [
    { opacity = 100% },
    { opacity = 0%; loop = "invert" }
];
```

No percentage keys — the parser distributes them: **first = 0%, last = 100%, middle = linearly interpolated**.

### The interpolated-array form

The array form also supports **mixing explicit and implicit percentages** by including `%` (or `percent`/`percentage`) properties inside the keyframe blocks:

```css
keyframes = !keyframes [
    { transform = vec2(0, 0) },          // implicit 0%
    33% = { transform = vec2(10px, -10px) },  // wait, this is map form
    { transform = vec2(0, 0) }           // implicit 100%
];
```

Actually the example file uses an alternative — putting a percentage directly via map syntax mixed with array entries. Looking at `BuildKeyframes`:

```csharp
case ArrayValue arr: {
    ...
    list.Add(new KeyframeStep(0, new Dictionary<string, IStyleValue>(block.Properties)));
    ...
}

if (value is not ArrayValue || list.Count <= 0) return list;
var explicitPoints = new List<(int index, double pct)>();
for (var i = 0; i < list.Count; i++)
    if (TryGetExplicitPercentage(list[i].Properties, out var pct))
        explicitPoints.Add((i, pct));
```

The post-processing for the array form scans for explicit percentages **inside the keyframe blocks** as `%`, `percent`, or `percentage` properties:

```css
keyframes = !keyframes [
    { opacity = 0 },                      // implicit 0%
    { opacity = 0.5; percent = 30% },     // explicit 30%
    { opacity = 1 }                       // implicit 100%
];
```

Internal interpolation logic:
1. First and last keyframes are pinned to 0% and 100% (explicit values overridden).
2. Between explicit points, intermediate frames are linearly distributed.

This is rarely used — the map form is clearer. It exists for tooling that generates animations programmatically.

## How animations are looked up

`StyleSheet.ComputedAnimations` is a `Dictionary<string, KeyframedAnimation>`. The host code that wants to play an animation does something like:

```csharp
var anim = styleSheet.ComputedAnimations["fade-in"];
element.PlayAnimation(anim);
```

The animation system then ticks the `KeyframedAnimation` per frame, interpolating each `Keyframe`'s properties via the `SteppingFunction`.

There's no automatic CSS-style `transition: opacity 0.3s` — animations are explicitly named and explicitly invoked. The DSL is descriptive (this animation exists); the host decides when to run it.

## Stepping functions

Resolved via `SteppingFunctions.ParseSteppingFunction(string)`. Common values (from CSS conventions):

| String | Behavior |
|---|---|
| `"linear"` | Constant rate. |
| `"ease-in"` | Slow start, fast end. |
| `"ease-out"` | Fast start, slow end. |
| `"ease-in-out"` | Slow start and end, fast middle. |
| `"step-start"` | Jumps immediately to end. |
| `"step-end"` | Stays at start until last instant. |

The actual list depends on `SteppingFunctions` (in `Sundex.Core.Animations`). Any unrecognised string typically throws or falls back to linear.

## Threading

`ParseAnimations` is pure CPU — runs synchronously inside `StyleSheet`'s constructor. No GL.

The animation **playback** is on the GL thread (it touches `Renderable.Position`, `Renderable.Scale`, etc., which are GPU-side properties). The conversion is one-shot at stylesheet construction; playback ticks happen in the render loop.

## Limits

- No `easing` parameters beyond named stepping functions (no custom `cubic-bezier(...)`).
- No automatic property-based interpolation triggers (no `transition:` shorthand).
- No keyframe negative-percentage / over-100% (clamped or rejected).
- No nested animation blocks.

## Related

- [[Style Types#KeyframesValue|KeyframesValue]] — the parser-side type.
- [[Blocks#animation|`animation` block]] — the top-level form.
- [[../Engine/Threading|Threading]] — when ticking happens.
- [[Style DSL|Style DSL]] — the index.
