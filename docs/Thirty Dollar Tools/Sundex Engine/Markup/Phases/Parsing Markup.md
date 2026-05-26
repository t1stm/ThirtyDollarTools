# Parsing Markup

The recursive heart of the builder. `BuildUIElement` walks a `SundexNode` tree, dispatches each tag to a `UIElement` constructor, applies attributes, and registers IDs/classes. Lives in `Sundex/Sundex.Markup/Builders/ComponentBuilderV1.cs`.

> Source: `Sundex/Sundex.Markup/Builders/ComponentBuilderV1.cs` (the `BuildUIElement` and helper methods).

## `BuildUIElement` — the dispatch

```csharp
private static UIElement BuildUIElement(
    SundexNode node,
    ISundexContext context,
    List<ISundexComponent>? dependencies,
    StyleSheet? styleSheet,
    Dictionary<string, UIElement> registeredIds,
    Dictionary<string, List<UIElement>> registeredClasses)
{
    var nodeTag = node.TagName;
    UIElement element;
    switch (nodeTag) {
        case "stack":    element = new StackPanel(context.UIContext) { Children = ... }; break;
        case "flex":     element = new FlexPanel(context.UIContext)  { Children = ... }; break;
        case "panel":    element = new Panel(context.UIContext)      { Children = ... }; break;
        case "label":    /* see below */                                                  break;
        case "progress": /* see below */                                                  break;
        case "button":   /* see below */                                                  break;
        default:         /* custom factory or imported component */                       break;
    }

    ApplyAttributes(element, node);
    if (node.Id is not null)      element.ID      = node.Id;
    if (node.Classes is not null) element.Classes = node.Classes;

    // Register ID
    if (!string.IsNullOrEmpty(node.Id)) registeredIds[node.Id] = element;

    // Register classes
    if (node.Classes is { Count: > 0 })
        foreach (var @class in node.Classes) {
            if (!registeredClasses.TryGetValue(@class, out var list))
                list = registeredClasses[@class] = [];
            list.Add(element);
        }

    return element;
}
```

Six built-in tags + a fallthrough for custom factories and imported components. Each case returns a `UIElement` with its children already constructed (recursive call in the children-list initialiser).

### Tag dispatch — the built-ins

| Tag | Maps to | Notes |
|---|---|---|
| `<stack>` | [[../../Components/Panels#StackPanel|`StackPanel`]] | Children built recursively. |
| `<flex>` | [[../../Components/Panels#FlexPanel|`FlexPanel`]] | Children built recursively. |
| `<panel>` | [[../../Components/Panels#Panel|`Panel`]] | Children built recursively. |
| `<label>` | [[../../Components/Labels#Label|`Label`]] | Reads `value="..."` attribute for initial text. |
| `<progress>` | [[../../Components/Bars#ProgressBar|`ProgressBar`]] | Reads `background`/`foreground` style values for plane defaults; `value="..."` for initial progress. |
| `<button>` | [[../../Components/Labels#Button|`Button`]] | Requires exactly one `<label>` child. |

### `<label>`

```csharp
case "label": {
    node.Attributes.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue("value", out var text);
    element = new Label(context.UIContext, text ?? string.Empty);
    break;
}
```

`<label value="Hello"/>` → `new Label(ctx, "Hello")`. Missing `value` → empty label. The `TryGetValue` uses the span lookup variant — saves an alloc for `"value"`.

### `<progress>`

```csharp
case "progress": {
    var background = ExtractBackgroundStyle(node, styleSheet);
    var foreground = ExtractBackgroundStyle(node, styleSheet, "foreground");

    if (node.Attributes.GetAlternateLookup<ReadOnlySpan<char>>()
            .TryGetValue("value", out var progressString) &&
        float.TryParse(progressString, out var progress))
        element = new ProgressBar(context.UIContext, background, foreground) {
            Progress = progress
        };
    else
        element = new ProgressBar(context.UIContext, background, foreground);
    break;
}
```

Two oddities:

1. **Style values are read at construction time.** Most tags get their styles applied later by `ApplyStyleSheet`. But `ProgressBar` needs a background plane *to construct its child panels with*, so the builder peeks at the stylesheet via `ExtractBackgroundStyle` before the constructor runs.

2. **`value` becomes `Progress`.** Even though the property is called `Progress` in C#, the markup uses `value` for symmetry with `<label value="...">`. Convention: most "primary content" attributes are called `value`.

### `<button>`

```csharp
case "button": {
    var labelNode = node.Children.FirstOrDefault(child => child.TagName == "label");
    if (labelNode is null) throw new Exception("Button must have a label");

    var background = ExtractBackgroundStyle(node, styleSheet);
    element = BuildUIElement(labelNode, context, dependencies, styleSheet, registeredIds, registeredClasses)
        is Label label
        ? new Button(context.UIContext, label, background)
        : throw new Exception("Button label wasn't parsed as a label.");
    break;
}
```

Buttons must have a `<label>` child. The label is built first, then handed to the `Button` constructor as the second argument:

```xml
<button>
    <label value="Save"/>
</button>
```

Becomes:

```csharp
var label = new Label(ctx, "Save");
var button = new Button(ctx, label, background);
```

The label-first construction means stylesheet rules targeting `button label` (descendant) work correctly — the label is a child of the button by the time `ApplyStyleSheet` runs.

The `is Label label` pattern handles the case where someone registered a custom `<label>` factory that returns a non-`Label` `UIElement` — the builder won't crash with a cast exception, it throws a clear error message.

### Default — custom factories and dependencies

```csharp
default: {
    // 1. Custom factories first
    var customElement = context.CreateElement(nodeTag);
    if (customElement != null) {
        element = customElement;
        if (element is Panel panel && node.Children.Count > 0)
            panel.Children.AddRange(node.Children
                .Select(child => BuildUIElement(child, context, dependencies, styleSheet,
                    registeredIds, registeredClasses)));
        break;
    }

    // 2. Imported component dependency
    if (dependencies is null) throw new Exception($"Unknown tag: {nodeTag}");
    var dependency = dependencies.FirstOrDefault(d => d.Name == nodeTag);
    element = dependency is not null
        ? dependency.Element
        : throw new Exception($"Unknown node tag: {nodeTag}");
    break;
}
```

Lookup order:

1. **Custom factory** registered via `SundexContext.RegisterElementFactory(tagName, factory)`. The factory returns a fresh element. If the result is a `Panel`, the markup's children are built and added. (Non-panel custom elements ignore children — there's nowhere to put them.)
2. **Imported component**. The `imports="['name']"` attribute on `<sundex>` populated `dependencies`. If the unknown tag matches a dependency `Name`, the dependency's *already-built* `Element` is reused. Note this means **the imported component's element is shared** — every `<header/>` in your markup points to the same `Header.Element` instance. Re-rendering it under multiple parents will produce undefined visual results. This is a known constraint; treat imports as singletons.
3. **Throw** with a clear "Unknown tag: ..." message.

The recursion on step 1 only happens for `Panel` subclasses — which means custom non-Panel components can't have markup children. This is intentional: a `<waveform/>` with children doesn't have a clear meaning. If you need children, make your custom element a `Panel`.

## `ApplyAttributes` — common attributes

```csharp
private static void ApplyAttributes(UIElement element, SundexNode node)
{
    foreach (var (key, value) in node.Attributes) switch (key) {
        case "width":
            element.Width = ParseLiteralOrComputable(value);
            break;
        case "height":
            element.Height = ParseLiteralOrComputable(value);
            break;
        case "padding":
            if (float.TryParse(value, out var p))
                if (element is IPositioningElement pe) pe.Padding = p;
            break;
        case "spacing":
            if (float.TryParse(value, out var s))
                switch (element) {
                    case StackPanel sp: sp.Spacing = s; break;
                    case FlexPanel fp:  fp.Spacing = s; break;
                }
            break;
        case "direction":
            if (Enum.TryParse<LayoutDirection>(value, true, out var dir))
                switch (element) {
                    case StackPanel sp: sp.Direction = dir; break;
                    case FlexPanel fp:  fp.Direction = dir; break;
                }
            break;
    }
}
```

Five attributes are recognised: `width`, `height`, `padding`, `spacing`, `direction`. Anything else — `class`, `id`, `value`, custom attrs — is silently ignored at this stage (handled elsewhere or by stylesheet).

### Why isn't this exhaustive?

The markup parser doesn't know about every `[NamedSetting]` property a component might have. The stylesheet does — it's how `font-size: 14;` finds its way to `Label.FontSizePx`. So `ApplyAttributes` only handles the attributes whose values are *position-or-layout-critical for the initial render* — the things you can't always express as a stylesheet rule because they depend on the markup structure.

The pattern in practice:

```xml
<flex direction="vertical" padding="10" spacing="5"
      class="settings_panel"
      width="100%"
      height="auto">
    ...
</flex>
```

`direction`/`padding`/`spacing`/`width`/`height` come through `ApplyAttributes`. Everything else (`background`, `font-size`, ...) is set via:

```css
flex.settings_panel {
    background: rgb(40, 40, 40);
    border-radius: 8;
}
```

### `Enum.TryParse<LayoutDirection>(value, true, ...)`

The `true` is `ignoreCase`. So `direction="Vertical"` and `direction="vertical"` both work.

### `IPositioningElement` for padding

The padding case checks `element is IPositioningElement pe` rather than concrete-typing. This is what lets future panel-like components opt in to padding without touching the parser — implement `IPositioningElement` and `ApplyAttributes` will set it.

`spacing` and `direction` use concrete `StackPanel`/`FlexPanel` checks because they currently only apply to those two — `Panel` (the base) doesn't have `Spacing` ([[../../Components/Panels#Panel|it does, actually, but only as a stylesheet-driven property]]). The mismatch is a small wart: `IPositioningElement` defines `Spacing` and `Direction`, so this could be unified. Currently the typed switch is preserved to avoid behavioural changes.

## `ParseLiteralOrComputable` — width/height value parser

```csharp
private static LiteralOrComputable ParseLiteralOrComputable(string value)
{
    if (value.EndsWith('%')) {
        if (float.TryParse(value[..^1], out var p))
            return new LiteralOrComputable(p, true);
    }
    else if (value.Equals("auto", StringComparison.OrdinalIgnoreCase)) {
        return new LiteralOrComputable(0, false, true);
    }
    else if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase)) {
        if (float.TryParse(value[..^2], out var v))
            return new LiteralOrComputable(v);
    }
    else {
        if (float.TryParse(value, out var v))
            return new LiteralOrComputable(v);
    }
    return new LiteralOrComputable(0);
}
```

Four formats supported:

| Markup | Result |
|---|---|
| `width="50%"` | `LiteralOrComputable(50, IsPercentage=true)` |
| `width="auto"` | `LiteralOrComputable(0, Auto=true)` |
| `width="100px"` | `LiteralOrComputable(100)` |
| `width="100"` | `LiteralOrComputable(100)` |
| `width="garbage"` | `LiteralOrComputable(0)` (silent fallback) |

The silent fallback to zero is debatable — a malformed `width` produces a zero-width element rather than a parse error. In practice this is fine because zero-width is visually obvious during development. A stricter parser would throw.

## `ExtractBackgroundStyle` — pre-emptive style peek

```csharp
private static Renderable? ExtractBackgroundStyle(SundexNode node, StyleSheet? styleSheet,
    string property = "background")
{
    var background = GetStyleForNode(node, property, styleSheet);
    return background switch {
        GradientValue gv => gv.GenerateGradientPlane(),
        ColorValue    cv => new ColoredPlane { Color = cv.Vector },
        _                => null
    };
}

private static IStyleValue? GetStyleForNode(SundexNode node, string property, StyleSheet? styleSheet)
{
    if (styleSheet is null) return null;
    var tagName = node.TagName;
    return styleSheet.GetStyleValueForTag(tagName, property);
}
```

Used only by `<progress>` and `<button>` to grab a `Renderable` for their constructors before the rest of `ApplyStyleSheet` runs. This is asymmetric — most tags get styles applied via the regular `ApplyStyleSheet` post-pass, but these two need the backgrounds *during construction*.

Trade-off: `GetStyleValueForTag` only matches by *tag name*, not by ID or class. So a `<button id="primary">` whose CSS is `button#primary { background: ...; }` won't get its specific background piped through to the constructor — it'll fall back to the tag-level rule. The post-pass `ApplyStyleSheet` will eventually apply the more specific rule, swapping the background.

This works correctly because `Button.ApplyStyleValue` (inherited from [[../../Components/Panels#Panel|`Panel`]]) handles background swaps via `HandleRenderableSwap`. The constructor-time background is essentially a placeholder for the moment between construction and `ApplyStyleSheet`.

## ID and class registration

```csharp
if (node.Id is not null)      element.ID      = node.Id;
if (node.Classes is not null) element.Classes = node.Classes;

if (!string.IsNullOrEmpty(node.Id)) registeredIds[node.Id] = element;

if (node.Classes is { Count: > 0 })
    foreach (var @class in node.Classes) {
        if (!registeredClasses.TryGetValue(@class, out var list))
            list = registeredClasses[@class] = [];
        list.Add(element);
    }
```

Two assignments per registered name:

1. **On the `UIElement`** — `element.ID` and `element.Classes`. These are what `ApplyStyleSheet` reads to match selectors.
2. **In the registries** — used later by `component.GetID<T>("...")` and `component.RegisteredClasses["..."]`.

The registry is needed because `UIElement.ID` is set-once on the leaf — there's no global "find me the element with this ID" without scanning the tree. The registry is that scan, done once at build time.

Registering uses **last-write-wins for IDs** (`registeredIds[node.Id] = element`) — duplicate IDs in markup will overwrite, with no warning. Don't.

For classes, the list is *additive* — duplicate class names within one element add the element multiple times to the same list. Probably a bug, but harmless if `class="primary"` is just used once per element.

## Threading

`BuildUIElement` is GL-thread only — every constructor it calls (`new Label`, `new Panel`, `new Button`) allocates GPU resources. See [[Component Builders|Component Builders]] for the round-trip pattern.

## Related

- [[../../Components/Components|Components]] — the target classes.
- [[Component Builders|Component Builders]] — the orchestrator that calls into `BuildUIElement`.
- [[Parsing Style|Parsing Style]] — the next phase (post-pass `ApplyStyleSheet`).
- [[../Component Definition#registration|Component registration]] — what the registry dictionaries feed into.
