# Abstractions

Every component in [Sundex.Components](Components.md) derives from `UIElement` and runs against a single `UIContext`. This page covers the abstract base class, the context, the value types (`LiteralOrComputable`, `ComputedRectangle`), and the small enums (`Align`, `Anchor`, `LayoutDirection`, `UIState`, `CursorType`).

> Source: `Sundex/Sundex.Components/Abstractions/`.

## `UIElement` (abstract)

The contract every component implements. Roughly 530 lines of base class — most of it is plumbing for layout, style application, and state transitions.

```csharp
public abstract class UIElement
{
    public abstract string Tag { get; }                // "panel", "label", "button"...
    public UIContext Context { get; }
    public string ID { get; set; } = "";
    public List<string> Classes { get; set; } = [];   // order matters — later class wins
    public virtual UIElement? Parent { get; set; }

    public virtual ComputedRectangle Computed { get; protected set; }

    [NamedSetting("animations")] public List<Animation> Animations { get; set; }
    [NamedSetting("x")]          public virtual LiteralOrComputable X { get; set; }
    [NamedSetting("y")]          public virtual LiteralOrComputable Y { get; set; }
    [NamedSetting("width")]      public virtual LiteralOrComputable Width  { get; set; }
    [NamedSetting("height")]     public virtual LiteralOrComputable Height { get; set; }
    [NamedSetting("anchor-x")]   public Anchor AnchorX { get; set; }
    [NamedSetting("anchor-y")]   public Anchor AnchorY { get; set; }
    [NamedSetting("visible")]    public bool   Visible { get; set; } = true;

    public bool IsHovered, IsPressed, UpdateCursorOnHover, NeedsLayout;
    public UIState CurrentState { get; private set; }
    public Action<UIElement>? OnClick { get; set; }

    // ... lifecycle ...
    public virtual (float w, float h) Measure(float parentW, float parentH);
    public virtual void Layout();
    protected virtual void DoLayout();
    public virtual void Test(MouseState mouse, Vector2 scale);
    public virtual void Update(UIContext);
    public virtual void DrawTo(UIContext);
    protected abstract void DrawSelf(UIContext);
    public virtual void StopRendering();

    // ... styling ...
    public virtual void ApplyStyleSheet(StyleSheet);
    protected virtual void ApplyStyleValue(StyleSheet, IStyleValue?, PropertyInfo);
    public virtual void InvalidateStyle();
    public virtual void ApplyStateOverride(StyleSheet, string state);
}
```

### The three layout phases

```
   parent.Layout()
       │
       ├─ child.Measure(innerW, innerH)  ─► returns desired size, used for Auto sizing
       │
       ├─ assign child.X / child.Y / maybe child.Width=...
       │
       └─ child.Layout()
              │
              ├─ Computed.UpdateAbsoluteBasedOnParent(this, Parent)
              │     resolves percentages, anchors, absolute positions
              │
              └─ DoLayout()
                    │
                    ├─ position background, set Viewport
                    │
                    └─ for each child: child.Layout()
```

`Measure` is **purely informational** — it never mutates the element. It's how a parent flex container can ask "if I gave you this much space, how big would you want to be?" before committing to a layout. `DoLayout` is where mutation happens.

This is why `UIElement.Measure` has a default that returns `(Width.Resolve(parentW), Height.Resolve(parentH))` for non-Auto values, but [`FlexPanel.Measure`](Panels.md#flexpanel) overrides it to actually walk children and sum/max their sizes.

### `InvalidateLayout` vs `InvalidateCoordinates`

Two separate dirty flags:

- **`InvalidateLayout`** sets `NeedsLayout = true`. The next `Layout()` call will run the full Measure → DoLayout cycle. This is what you call when *sizes* have changed.
- **`InvalidateCoordinates`** is cheaper — it just calls `Computed.UpdateAbsoluteBasedOnParent`, recomputing absolute X/Y without re-running `DoLayout`. This is what you call when only the *parent* moved, since child relative positions haven't changed.

`UpdateSetDirty(ref field, value)` is the standard property setter helper — assigns the new value and calls `InvalidateLayout()` only if the value actually changed. It's used pervasively: see [`Panel.BorderRadius`](Panels.md#panel), `Panel.Padding`, etc.

### Hit testing — `Test(MouseState, Vector2 scale)` and `UIContext.UpdatePointer`

As of the July 2026 input-routing rework, `Test` no longer resolves hover/press/click itself — it's a thin forward to the context, only acting on root elements (elements with no `Parent`):

```csharp
public virtual void Test(MouseState mouse, Vector2 scale)
{
    if (!Visible) return;

    if (Parent == null)
        Context.UpdatePointer(this,
            mouse.X, mouse.Y,
            mouse.IsButtonDown(MouseButton.Left),
            mouse.IsButtonPressed(MouseButton.Left),
            mouse.IsButtonReleased(MouseButton.Left),
            mouse.ScrollDelta,
            mouse.IsButtonDown(MouseButton.Right));
}
```

`UIContext.UpdatePointer` is the single place all pointer logic now lives — resolving the topmost hit (occlusion), maintaining hover/pressed chains, capture, clicks, wheel routing, and click-to-focus, across every root sharing the context:

- **Hit resolution** — `CapturedElement ?? root.HitTest(x, y)`. `HitTest` is `virtual` on `UIElement` (default: `ContainsPoint(x, y) ? this : null`); **container** elements override it to test children and return the topmost by `Index`. Elements that sit outside the normal parent/child tree (e.g. popovers) need their own `HitTest` override to be reachable at all.
- **Capture** — while `CapturedElement` is set (from a press), it wins hit resolution outright, and a capture owned by a different root tree makes that tree unreachable for the pointer.
- **Press / focus** — on `wasPressed`, walks `winner` → `Parent` chain calling `HandlePress`; the first element that handles it (or the raw hit) becomes `CapturedElement`. Separately walks the same chain for the first `Focusable` element and calls `Focus`/`Blur`. Also detects double-clicks (`DoubleClickMs` + pixel slop) and dispatches `HandleDoublePress`.
- **Right-press** — level-triggered (fires every update while held, not just on press), so drag-to-erase style gestures work; handlers must be idempotent.
- **Click** — on `wasReleased`, if the captured element still contains the point, walks `captured` → `Parent` and invokes the first `OnClick` found (release-inside, bubbling to the first ancestor with a handler — not necessarily the element that was hit).
- **`ApplyPointerState()`** — diffs the old vs. new hover/press chains (root → target, via `Parent`), sets `IsHovered`/`IsPressed` and calls `SyncPointerState()` (which recomputes `CurrentState` — whose setter calls `InvalidateStyle()` on change, so `:hover`/`:pressed` stylesheet rules still apply automatically), and fires `OnHoverEnter`/`OnHoverExit` on transition.

Notes:

- `scale` is currently unused by the new `Test`/`UpdatePointer` path — mouse coordinates are consumed as-is (window pixels).
- `UpdatePointer` can be called directly with primitives (no `MouseState` needed) — useful for headless tests.
- For elements whose visual "hover glow" is driven by `PropagateAlpha`, don't add `state[hovered]` stylesheet blocks — use the `OnHoverEnter`/`OnHoverExit` callbacks and touch RGB only, since `PropagateAlpha` already owns alpha.

### Style application — the `[NamedSetting]` flow

`ApplyStyleSheet(styleSheet)` walks the `StyleSheet` looking for selectors that match this element's tag, ID, and classes. For each match it grabs the property values and:

1. Reflects over `this.GetType()` for properties decorated with `[NamedSetting]`.
2. For each one, looks up the named value in the style sheet.
3. If the value's `IStyleValue` type matches the property's CLR type, calls `propertyInfo.SetValue(this, value)` — typically through `ApplyStyleValue` which is the per-element override hook.
4. Snapshots the values into `_baseSnapshot` so state overrides can be reverted.

The override hook is what lets [`Panel`](Panels.md#panel) do special-case handling for `GradientValue` → `GradientPlane` and `ColorValue` → `ColoredPlane`:

```csharp
protected override void ApplyStyleValue(...) {
    switch (styleValue) {
        case GradientValue gv when propertyInfo.PropertyType == typeof(Renderable):
            propertyInfo.SetValue(this, gv.GenerateGradientPlane());
            return;
        case ColorValue cv when propertyInfo.PropertyType == typeof(Renderable):
            propertyInfo.SetValue(this, new ColoredPlane { Color = cv.Vector });
            return;
        default:
            base.ApplyStyleValue(...);
            return;
    }
}
```

### State overrides — `:hover`, `:pressed`

When `IsHovered`/`IsPressed` flip (via `SyncPointerState()`, called from `UIContext.ApplyPointerState`), the `CurrentState` property setter detects the change and calls `InvalidateStyle()`, which:

1. Restores all `[NamedSetting]` properties from `_baseSnapshot` (so prior `:hover` mutations are undone).
2. Calls `ApplyStateOverride(stylesheet, "hover")` if hovered, or `"pressed"` if pressed.
3. The override walks selectors marked `:hover`/`:pressed` and applies them on top.

This way you get CSS-style state styling without explicit transition machinery.

### Render queue interaction

`UIElement` manages its position in the layered render queue indirectly. The base class exposes:

- `Index` — the element's z-order. Set through the `Parent` setter (`Index = Parent?.Index + 1 ?? 0`) and cascaded to children when a `Panel`'s own `Index` changes (see [`ProgressBar.UpdatePanelIndices`](Bars.md#progressbar) which assigns sub-indices).
- `Viewport` — `(int x, int y, int xw, int yh)` set by `DoLayout`. The renderer uses this for `glScissor` if needed.
- `DrawTo(context)` — pushes this element's renderables onto the queue and recurses to children.
- `DrawSelf(context)` — abstract, the per-element "queue *my* renderables" hook.
- `StopRendering()` — abstract, removes renderables from the queue (used on `Visible = false` and child removal).

`HandleRenderableSwap(oldValue, newValue, propertyName)` is a helper that:

1. Removes `oldValue` from the render queue (via `Context.DequeueRender`).
2. Queues `newValue` at the same index (via `Context.QueueRender`).
3. Looks up the `[RenderPriority]` attribute on `propertyName` to figure out which sub-queue to insert into.

Used everywhere a stylesheet swap might replace a `Renderable`-typed property.

## `UIContext`

```csharp
[PreloadGraphicsContext]
public class UIContext : IGamePreloadable
{
    protected readonly List<List<IRenderable>> LayeredRenderQueue = [];
    public required Camera Camera { get; set; }
    public IAssetProvider AssetProvider;
    public IFontProvider  FontProvider;
    public ITextProvider  TextProvider;
    public DeleteQueue    DeleteQueue;
    public Action<CursorType> RequestCursor;

    public int ViewportWidth, ViewportHeight;

    public static void Preload(AssetProvider assetProvider) {
        _fontProvider = new FontProvider(assetProvider);
        _textProvider = new TextProvider(assetProvider, _fontProvider, "Lato Bold");
    }

    public void QueueRender(IRenderable r, int renderIndex, int queueIndex = -1);
    public int  DequeueRender(IRenderable r, int renderIndex);
    public void RegisterUpdate(UIElement);
    public void UnregisterUpdate(UIElement);
    public void Render();
}
```

### The layered render queue

```
LayeredRenderQueue: List<List<IRenderable>>
    [0]: [bg of element 0, content of element 0]
    [1]: [bg of element 1, content of element 1]
    [2]: [bg of element 2]
    ...
```

Each *outer* index = an element's `Index` (z-order). Each *inner* list is the renderables for that element, ordered by `[RenderPriority]` ascending (background first → foreground).

`QueueRender(r, renderIndex, queueIndex = -1)`:
- `renderIndex` — outer index, the element's `Index`.
- `queueIndex` — inner position, defaults to "append at the end" (`-1`). Most components don't bother — `Panel.Background` does, with `[RenderPriority(0)]`, to go to the front of its sub-queue.

### Per-element update registration

```csharp
private readonly HashSet<UIElement> _updatingElements = new();
public void RegisterUpdate(UIElement e)   => _updatingElements.Add(e);
public void UnregisterUpdate(UIElement e) => _updatingElements.Remove(e);
```

Elements with active animations register themselves. `Render()` iterates `_updatingElements` first (calling `e.Update(this)` per-element) and *then* drains the render queue. This way animations are guaranteed to run before the elements they animate are drawn that frame.

### `Preload` — default font

`UIContext` is `[PreloadGraphicsContext]` and its `Preload` constructs a `FontProvider` and a `TextProvider` for the default font ("Lato Bold"). This is what makes `Label`s render without any setup — by the time a `Label` is constructed, the [`TextProvider`](../Engine/Text%20Rendering/Text%20Rendering.md) already exists.

If you want a different default font, you'd need to override `Preload` or subclass `UIContext`.

## Value types

### `LiteralOrComputable`

```csharp
public readonly struct LiteralOrComputable(float value, bool isPercentage = false, bool auto = false)
{
    public float Value;
    public bool  IsPercentage;
    public bool  Auto;

    public static readonly LiteralOrComputable AutoSize = new(0, false, true);

    public float Resolve(float reference) =>
        IsPercentage ? reference * (Value / 100f) : Value;

    public static implicit operator LiteralOrComputable(float value) => new(value);
    public static LiteralOrComputable Percent(float i) => new(i, true);
}
```

A tagged union of three states:

- **Literal** — `Value` is a pixel count. `Resolve(_)` returns it as-is.
- **Percentage** — `Value` is 0-100. `Resolve(parentSize)` scales it.
- **Auto** — `Value` is unused. The owning element's `Measure` decides the size.

Implicit conversion from `float` means most call sites can write `Width = 100` instead of `Width = new LiteralOrComputable(100)`. Percentages need the explicit `LiteralOrComputable.Percent(50)` or `new(50, true)`.

`AutoSize` is a singleton readonly struct used as a default for [`FlexPanel.Width`/`Height`](Panels.md#flexpanel) — flex panels shrink-wrap their children unless explicitly sized.

### `ComputedRectangle`

```csharp
public class ComputedRectangle
{
    public Action? OnUpdate { get; set; }
    public float AbsoluteX { get; private set; }   // window-space
    public float AbsoluteY { get; private set; }
    public float X { get; private set; }           // parent-relative (after padding)
    public float Y { get; private set; }
    public float Width { get; private set; }
    public float Height { get; private set; }

    public void UpdateAbsoluteBasedOnParent(UIElement current, UIElement? parent);
    public void OverrideAbsolutePositions(float x, float y);
}
```

No constructor — it's created empty. Deliberately: it's instantiated from the `UIElement` constructor, before virtual `Width`/`Height` overrides (e.g. `WindowFrame` → `Container.Width`) have run, so measuring there would be premature. The element starts with `NeedsLayout = true`, so the first `Layout()` call fills it in via `UpdateAbsoluteBasedOnParent`. The resolved geometry is recomputed every layout pass and lives on `UIElement.Computed`.

`UpdateAbsoluteBasedOnParent` does the actual resolution:

1. Get parent's inner dimensions: `(parentW - 2*padding, parentH - 2*padding)`.
2. If `Auto`, ask the element via `Measure(innerW, innerH)`.
3. Otherwise, `Resolve` against inner dimensions (literal or %).
4. Resolve `X`/`Y` *relative to parent's content origin* (after padding).
5. Apply anchor offsets — `AnchorX = Center` shifts X left by `Width/2`.
6. Compute `AbsoluteX/Y` by walking up: parent's absolute origin + parent's padding + this element's relative X.
7. Fire `OnUpdate` if any value changed.

`OnUpdate` is the hook background renderables can use to know when their backing rect has moved (e.g. for invalidating a gradient cache).

## Enums

```csharp
public enum Align        { Start, Center, End, Stretch }
public enum Anchor       { Start, Center, End }
public enum LayoutDirection { Horizontal, Vertical }
public enum UIState      { None, Hovered, Pressed }
public enum CursorType   { Default, Pointer, Text, ResizeX, ResizeY }
```

### `Align` vs `Anchor`

The distinction trips first-time readers up:

- **`Align`** is what a *parent* uses to position children inside its inner area. `FlexPanel.HorizontalAlign = Center` means "centre all children inside me." Has a `Stretch` value.
- **`Anchor`** is what an *element* uses to interpret its own `X`/`Y` — `AnchorX = Center` means "treat my X as the centre of my width, not the left edge."

`Stretch` only makes sense for `Align` — there's nothing to stretch about a self-anchor.

### `CursorType`

```csharp
public enum CursorType { Default, Pointer, Text, ResizeX, ResizeY }
```

Requested via `UIContext.RequestCursor(CursorType)` from `Update()` when `IsHovered && Cursor != CursorType.Default`. `UpdateCursorOnHover` is a compatibility property backed by the `[NamedSetting("cursor")]` `Cursor` property (`get => Cursor != CursorType.Default; set => Cursor = value ? CursorType.Pointer : CursorType.Default;`). The host (`Game`) maps the request to GLFW cursor handles. Default is `Default`; on no-hover the request reverts.

## Marker interfaces

### `IColoredBackground`

```csharp
public interface IColoredBackground {
    [NamedSetting("background")] Renderable? Background { get; set; }
}
```

A capability marker — anything with a settable background. `Panel` implements it. The stylesheet machinery uses the interface (rather than the concrete `Panel`) so other components can opt in without inheriting `Panel`.

### `IPositioningElement`

```csharp
public interface IPositioningElement {
    [NamedSetting("direction")] LayoutDirection Direction { get; set; }
    [NamedSetting("padding")]   float Padding { get; set; }
    [NamedSetting("spacing")]   float Spacing { get; set; }
}
```

Marker for "I lay out my children." [`ComputedRectangle.UpdateAbsoluteBasedOnParent`](Abstractions.md#computedrectangle) checks `parent is IPositioningElement pe` to decide whether to subtract `pe.Padding` from the inner area — non-positioning containers don't have "padding" in the layout sense.

## Attributes

### `[NamedSetting(name)]`

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class NamedSettingAttribute(string name) : Attribute {
    public string Name { get; } = name;
}
```

Decorates a property with the name it's bound to in stylesheets. `[NamedSetting("font-size")]` → `font-size: 14;` in the stylesheet → `propInfo.SetValue(element, 14)`.

The reflection happens once in `ApplyStyleSheet`, but typical UIs call this many times per second (state changes, theme swaps), so the property lookups are cached internally.

### `[RenderPriority(N)]`

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class RenderPriorityAttribute(int priority) : Attribute {
    public int Priority { get; } = priority;
}
```

Sub-queue ordering within an element's render slot. Lower = earlier (further back). `Panel.Background` is `[RenderPriority(0)]`. Children render later (no priority means appended).

This is *not* z-order across elements — that's the element's `Index`. This is just "within my own render slot, what order are my own renderables in?"

## Related

- [Panel](Panels.md) is the simplest concrete subclass; reading `UIElement.cs` then `Panel.cs` gives you the full layout/style story.
- [TextProvider](../Engine/Text%20Rendering/Text%20Rendering.md) is what `UIContext.Preload` instantiates.
- [Style DSL](../Style%20DSL/Style%20DSL.md) is the source of `IStyleValue` / `StyleSheet` / `Animation`.
- [Markup](../Markup/Markup.md) is what *constructs* trees of `UIElement` from disk.
