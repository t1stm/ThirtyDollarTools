# Panels

`Panel` is the foundational container component. `FlexPanel` is the layout workhorse with align/justify semantics. `StackPanel` is a simpler stacking layout. `WindowFrame` is a draggable, resizable window built from the others.

> Source: `Sundex/Sundex.Components/Panels/`.

## `Panel`

```csharp
public class Panel(UIContext context) : UIElement(context),
    IColoredBackground, IPositioningElement
{
    [RenderPriority(0)]
    [NamedSetting("background")] public Renderable? Background { get; set; }

    [NamedSetting("border-radius")] public LiteralOrComputable BorderRadius { get; set; } = 0;
    [NamedSetting("direction")]     public virtual LayoutDirection Direction { get; set; } = LayoutDirection.Horizontal;
    [NamedSetting("padding")]       public virtual float Padding { get; set; } = 0;
    [NamedSetting("spacing")]       public virtual float Spacing { get; set; } = 0;

    public List<UIElement> Children { get; set; } = [];

    public override string Tag => "panel";
}
```

A `Panel` has:

- **A background** — any `Renderable` (typically `ColoredPlane` or `GradientPlane`).
- **Children** — laid out by overrides of `DoLayout` (the base `Panel.DoLayout` does *not* lay out children directly — it just positions itself; subclasses like `FlexPanel` do the per-child arithmetic).
- **Style state** — `BorderRadius`, `Direction`, `Padding`, `Spacing`.

It implements both [[Abstractions#IColoredBackground|`IColoredBackground`]] and [[Abstractions#IPositioningElement|`IPositioningElement`]].

### `Children` and parent linking

```csharp
public List<UIElement> Children {
    get => _children;
    set {
        _children = value;
        SetChildrenParent();
        InvalidateLayout();
    }
}
public override UIElement? Parent {
    get => base.Parent;
    set {
        base.Parent = value;
        SetChildrenParent();
    }
}
protected void SetChildrenParent() {
    foreach (var child in Children) child.Parent = this;
}
public virtual void AddChild(UIElement child) {
    if (child.Parent is Panel oldParent) oldParent.RemoveChild(child);
    _children.Add(child);
    child.Parent = this;
    child.DrawTo(Context);          // queue renderables NOW
    InvalidateLayout();
}
public void RemoveChild(UIElement child) {
    _children.Remove(child);
    child.Parent = null;
    child.StopRendering();          // dequeue renderables NOW
    InvalidateLayout();
}
```

Two reasons the parent linkage is set **eagerly**, not lazily:

1. **Layout chains** — `child.Layout()` reads `child.Parent.Computed` to resolve percentages, so the parent reference must exist before the first layout pass.
2. **Reparenting** — `AddChild` checks if the child already has a `Panel` parent and removes it from there first. This way moving a child between panels is a single call.

### `DoLayout` — base behaviour

```csharp
protected override void DoLayout()
{
    var x = (int)Computed.AbsoluteX;
    var y = (int)Computed.AbsoluteY;
    Viewport = (x, y, x + (int)Computed.Width, y + (int)Computed.Height);

    if (Background is IBorderRadius br)
        br.BorderRadius = BorderRadius.Resolve(Computed.Height);

    Background?.SetPosition((x, y, 0));
    Background?.Scale = (Computed.Width, Computed.Height, 1);

    foreach (var child in Children) child.Layout();
}
```

`DoLayout` does three things in order:

1. **Set the viewport** — used for clipping if a parent enables scissoring.
2. **Position the background** — same rect as the panel, with `BorderRadius` resolved against height (so `border-radius: 50%` on a 40px-tall panel = 20px corner radius).
3. **Recurse into children** — the **default** has children at `(0, 0)` (the parent's content origin); subclasses overwrite child `X`/`Y` *before* calling `child.Layout()` to position them.

Critically, the base `Panel.DoLayout` does **not** override child `X`/`Y` — children stay at whatever the markup or stylesheet set them to. This makes `Panel` an "absolute positioning" container by default. To get flex-style auto-layout, use `FlexPanel`.

### Background swapping via stylesheet

The stylesheet might set `background: gradient(...)` or `background: red;`. `Panel.ApplyStyleValue` translates the `IStyleValue` to a `Renderable`:

```csharp
case GradientValue gv when propertyInfo.PropertyType == typeof(Renderable):
    var gradient = gv.GenerateGradientPlane();
    gradient.BorderRadius = BorderRadius.Resolve(Computed.Height);
    propertyInfo.SetValue(this, plane = gradient);
    break;

case ColorValue cv when propertyInfo.PropertyType == typeof(Renderable):
    var colored = new ColoredPlane {
        Color = cv.Vector,
        BorderRadius = BorderRadius.Resolve(Computed.Height)
    };
    propertyInfo.SetValue(this, plane = colored);
    break;
```

`HandleRenderableSwap(oldValue, plane, propertyName)` is then called to dequeue the old `Renderable` and queue the new one in its place. This is what lets a `:hover` selector swap a gradient for a flat colour without flicker.

### Hit testing recurses

```csharp
public override void Test(MouseState mouse, Vector2 scale) {
    if (!Visible) return;
    base.Test(mouse, scale);                // self
    foreach (var child in Children)
        child.Test(mouse, scale);           // recurse
}
```

A child's `IsHovered` is *independent* of its parent's. Both can be hovered simultaneously (the mouse is over both). This is by design — a hover style on a button inside a hovered panel both fire.

## `FlexPanel`

The CSS-flex inspired layout container. Adds horizontal/vertical alignment, spacing-aware free-space distribution, and optional wrapping.

```csharp
public class FlexPanel(UIContext context) : Panel(context)
{
    [NamedSetting("horizontal-align")] public Align HorizontalAlign { get; set; } = Align.Start;
    [NamedSetting("vertical-align")]   public Align VerticalAlign   { get; set; } = Align.Start;
    [NamedSetting("wrap")]             public virtual bool Wrap     { get; set; } = false;

    [NamedSetting("width")]  public override LiteralOrComputable Width  { get; set; } = LiteralOrComputable.AutoSize;
    [NamedSetting("height")] public override LiteralOrComputable Height { get; set; } = LiteralOrComputable.AutoSize;
}
```

### Why `Auto` is the default

`Panel.Width`/`Height` come from `UIElement` and default to whatever the parent's resolution decides. `FlexPanel` overrides both to `AutoSize` — a flex panel that's not given an explicit size **shrink-wraps to its content**.

Without this default you'd need to give every flex container an explicit size, which is wrong for the common case of "a button is as wide as its label + padding."

### `Measure` — the size resolution

`FlexPanel.Measure(parentW, parentH)` is ~80 lines of arithmetic but the structure is:

```
explicitW = Width.Auto ? null : Width.Resolve(parentW)
explicitH = Height.Auto ? null : Height.Resolve(parentH)
innerW = max(0, (explicitW ?? parentW) - 2*Padding)
innerH = max(0, (explicitH ?? parentH) - 2*Padding)

contentW, contentH := walk children (Wrap-aware) summing/maxing on the layout axis

return (
    explicitW ?? contentW + 2*Padding,
    explicitH ?? contentH + 2*Padding
)
```

The "walk children" is direction-and-wrap dependent:

| Direction | Wrap | Content size |
|---|---|---|
| Horizontal | No | sum of widths + spacing, max of heights |
| Horizontal | Yes | longest line width, sum of line heights |
| Vertical | No | sum of heights + spacing, max of widths |
| Vertical | Yes | sum of column widths, longest column height |

The recursive `child.Measure(innerW, innerH)` calls let nested flex panels propagate constraints down so a child can decide *its own* desired size given how much room the parent's giving it.

### `DoLayout` — the actual positioning

```csharp
protected override void DoLayout() {
    base.DoLayout();    // positions self + background, calls child.Layout() once with default positions
    var count = Children.Count;
    var inner_width  = Computed.Width  - 2 * Padding;
    var inner_height = Computed.Height - 2 * Padding;
    if (count < 1) return;
    if (Direction == LayoutDirection.Horizontal)
        Layout_Horizontal(count, inner_width, inner_height);
    else
        Layout_Vertical(count, inner_height, inner_width);
}
```

The non-wrap case is a **three-pass** algorithm — non-obvious enough to spell out:

```csharp
total_fixed   = sum of child widths where (!IsPercentage && !Auto)
total_auto    = sum of child.Measure().width where Auto
total_percent = sum of child width values where IsPercentage    // raw 0..100 values
total_spacing = Spacing * (count - 1)
free_space    = max(0, inner_width - total_fixed - total_auto - total_spacing)

offset = HorizontalAlign switch {
    Align.Center => (inner_width - total_width - total_spacing) / 2,
    Align.End    => inner_width - total_width - total_spacing,
    _            => 0
}

for each child:
    if (child.Width.IsPercentage)
        child.Width = child.Width.Value / 100f * free_space   // resolve % against remaining space
    child.X = offset
    child.Layout()
    // VerticalAlign: set child.Y based on inner_height vs child.Computed.Height
    child.Layout()    // re-layout if Y changed
    offset += child.Computed.Width + Spacing
```

The interesting part: **percentages are resolved against `free_space`, not `inner_width`**. This is what makes `width: 50%` mean "half of whatever's left after fixed-size siblings", which is the CSS-flex behaviour and what users expect.

The double `child.Layout()` is sub-optimal — the second call is needed because setting `child.Y` after `Layout()` would otherwise miss the `DoLayout` hook. Could probably be tightened with a `LayoutWithoutDoLayout` separation, but the current shape is correct.

### Wrap

`Wrap = true` switches to a wrapping layout (`Layout_Horizontal_Wrap` / `Layout_Vertical_Wrap`):

```csharp
private void Layout_Horizontal_Wrap(float innerWidth, float innerHeight) {
    float currentX = 0, currentY = 0, lineHeight = 0;
    var firstInLine = true;
    foreach (var child in Children) {
        var (cw, _) = child.Measure(innerWidth, innerHeight);
        if (!firstInLine && currentX + Spacing + cw > innerWidth && innerWidth > 0) {
            currentX = 0;
            currentY += lineHeight + Spacing;
            lineHeight = 0;
        }
        child.X = currentX;
        child.Y = currentY;
        child.Layout();
        currentX += child.Computed.Width + Spacing;
        lineHeight = Math.Max(lineHeight, child.Computed.Height);
        firstInLine = false;
    }
}
```

Note the wrap path **does not** support `HorizontalAlign`/`VerticalAlign` or percentage widths — wrapped layouts are always left-aligned, top-aligned, fixed-width. This is a deliberate scope cut; CSS-style "justify-content with wrap" is a much bigger algorithm.

### Style overrides

`FlexPanel.ApplyStyleValue` adds string-to-enum conversions for `Align` and `LayoutDirection`:

```csharp
case StringValue sv when propertyInfo.PropertyType == typeof(Align):
    Align? align = sv.Value switch {
        "center" => Align.Center, "end" => Align.End,
        "stretch" => Align.Stretch, "start" => Align.Start, _ => null
    };
    if (align is not null) propertyInfo.SetValue(this, align.Value);
    return;
```

This is what lets stylesheets write `horizontal-align: center;` instead of needing a typed enum literal. Same trick is used for `direction: horizontal | vertical`.

## `StackPanel`

The simpler cousin: stacks children along `Direction`, no align, no wrap.

```csharp
public class StackPanel(UIContext context) : Panel(context)
{
    public override string Tag => "stack";

    protected override void DoLayout() {
        base.DoLayout();
        float offset = 0;
        foreach (var child in Children) {
            if (Direction == LayoutDirection.Vertical) {
                child.X = 0;
                child.Y = offset;
                child.Layout();
                offset += child.Computed.Height + Spacing;
            } else {
                child.X = offset;
                child.Y = 0;
                child.Layout();
                offset += child.Computed.Width + Spacing;
            }
        }
    }
}
```

`Measure` is the FlexPanel non-wrap arithmetic without alignment — sum on layout axis, max on cross axis.

When to pick one over the other:

- Use **`Panel`** for absolute positioning (children with explicit `X`/`Y`).
- Use **`StackPanel`** when you want a simple stacked layout with no alignment.
- Use **`FlexPanel`** when you want align/justify semantics or wrapping.

The Visualizer uses `FlexPanel` ~95% of the time.

## `WindowFrame`

A draggable, resizable, optionally-decorated window panel. Owns a header (drag region) and a content panel.

```csharp
public class WindowFrame : Panel
{
    protected readonly Panel     Container;   // hosts header + child
    protected readonly FlexPanel Header;      // drag handle
    private UIElement? _child;
    private byte _resizingXMode, _resizingYMode;
    public bool Resizable { get; set; }

    public UIElement? Child {
        get => _child;
        set { if (value != null) SetChild(value); else _child = null; }
    }
}
```

The construction is:

```
WindowFrame
└── Container (FlexPanel, vertical)
    ├── Header (FlexPanel, horizontal, padding=10, dark grey background)
    │   └── Label "X"      (close button — currently inert in the base class)
    └── Child               (caller-provided content)
```

`WindowFrame.Width`/`Height` are forwarded to `Container` — the visible size *is* the container's size; the frame is otherwise transparent.

### Dragging via header

```csharp
public override void Test(MouseState mouse, Vector2 scale) {
    base.Test(mouse, scale);
    if (Header.IsPressed)
        ComputeHeaderPressed(mouse);
    else if (_resizingXMode != 0 || _resizingYMode != 0)
        HandleActiveResize(mouse);
    else if (Resizable && IsHovered)
        ComputeResize(mouse);

    if (mouse.IsButtonDown(MouseButton.Left)) return;
    _resizingXMode = 0;
    _resizingYMode = 0;
}

protected void ComputeHeaderPressed(MouseState mouse) {
    X = Computed.X + mouse.Delta.X;
    Y = Computed.Y + mouse.Delta.Y;
}
```

Three input modes:

1. **Header pressed** — drag the whole window by `mouse.Delta`.
2. **Active resize** — already in a resize mode, keep resizing until release.
3. **Hovered + resizable** — within 10px of an edge, start a resize on press.

The `_resizingXMode` / `_resizingYMode` byte flags encode three states each: `0` = no resize, `1` = resizing toward the positive end (right/bottom), `2` = resizing toward the negative end (left/top). The latter case both moves and resizes — dragging the left edge moves `X` while increasing `Width`.

### Resize hit zones

```csharp
const float rt = 10; // px
var x_negative = mx > x - rt && mx <= x + rt;     // left edge
var x_positive = mx >= xw - rt && mx < xw + rt;   // right edge
var y_negative = my > y && my <= y + rt;          // top edge
var y_positive = my >= yh - rt && my < yh + rt;   // bottom edge
```

10px hit zone on each side; if the mouse is inside one *and* `mouse.IsButtonDown(Left)`, the corresponding mode byte gets set.

### Cursor feedback

```csharp
protected CursorType RequestedCursor;
public override void Update(UIContext uiContext) {
    if (RequestedCursor != CursorType.Normal)
        uiContext.RequestCursor.Invoke(RequestedCursor);
}
```

The base class doesn't currently set `RequestedCursor` on resize hover (see source — it's a TODO-shape). Subclasses can extend `ComputeResize` to set `RequestedCursor = CursorType.ResizeX/Y` based on which edge the mouse is near.

## Threading

All `Panel` subclasses are GL-thread only. `AddChild` / `RemoveChild` mutate the children list and the render queue, so they must run on the main loop. [[../Engine/Threading|`ThreadRunner`]] users round-trip back via `Game.Enqueue` to add children produced off-thread (see [[Labels#FileSelection|`FileSelection`]] for the canonical example, which uses a `SemaphoreSlim` to commit children-list assignments).

## Related

- [[Abstractions|UIElement]] — the contract `Panel` implements.
- [[Bars|ProgressBar]] — uses two child `Panel`s for fg/bg; reads BorderRadius into both.
- [[Labels#Button|Button]] — derives from `FlexPanel` with `HorizontalAlign = Center`.
- [[Labels#DropDownLabel|DropDownLabel]] — uses a `Panel` parent + a hidden `FlexPanel` child.
