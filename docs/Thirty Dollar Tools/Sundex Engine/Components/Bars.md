# Bars

`ProgressBar` is the only "bar" component. Two stacked panels — a background and a foreground — with the foreground's width driven by a `Progress` value in `[0, 1]`. Border radius and stylesheet-driven background swaps propagate to both child panels.

> Source: `Sundex/Sundex.Components/Bars/ProgressBar.cs`.

## `ProgressBar`

```csharp
public class ProgressBar : UIElement
{
    public ProgressBar(UIContext context, Panel backgroundPanel, Panel foregroundPanel) : base(context) {
        BackgroundPanel = backgroundPanel;
        ForegroundPanel = foregroundPanel;
    }

    public ProgressBar(UIContext context, Renderable? bgPlaneBackground = null, Renderable? fgPlaneBackground = null)
        : this(context,
            new Panel(context) {
                Background = bgPlaneBackground,
                Width  = new LiteralOrComputable(100, true),
                Height = new LiteralOrComputable(100, true)
            },
            new Panel(context) {
                Background = fgPlaneBackground,
                Width  = new LiteralOrComputable(0, true),
                Height = new LiteralOrComputable(100, true)
            })
    { }

    [NamedSetting("background")]   public Panel BackgroundPanel { get; set; }
    [NamedSetting("foreground")]   public Panel ForegroundPanel { get; set; }
    [NamedSetting("border-radius")] public LiteralOrComputable BorderRadius { get; set; }
    [NamedSetting("progress")]     public float Progress { get; set; }

    public override string Tag => "progress";
}
```

Two constructors:

- **`(context, Panel, Panel)`** — full control. Pass pre-configured panels.
- **`(context, Renderable?, Renderable?)`** — convenience. Wraps the renderables in 100% × 100% / 0% × 100% panels.

The convenience constructor is what most callers use. The 0%-width foreground starts invisible; setting `Progress` later resizes it.

## How the layered panels work

```
 ProgressBar (Index = N)
 ├── BackgroundPanel (Index = N+1)  — full width, behind
 └── ForegroundPanel (Index = N+2)  — Progress*100 % width, in front
```

Both panels have:
- `Width = 100%` (background) / `Width = Progress*100 %` (foreground)
- `Height = 100%`
- Same `BorderRadius` (propagated from the parent)

The progress bar itself draws nothing — `DrawSelf` just calls `DrawTo` on each child panel.

### Why two `[NamedSetting]` Panel properties?

The `BackgroundPanel` and `ForegroundPanel` properties expose the panels themselves to the stylesheet — so a stylesheet can swap them entirely:

```css
progress {
    background: rgb(50, 50, 50);     /* sets BackgroundPanel via ApplyStyleValue */
    foreground: gradient(...);       /* sets ForegroundPanel  */
    border-radius: 4;
    progress: 0.5;                   /* literal value is a float */
}
```

The setter does some bookkeeping:

```csharp
[NamedSetting("background")]
public Panel BackgroundPanel {
    get;
    set {
        UpdateSetDirty(ref field, value);
        field.Parent = this;
        UpdatePanelIndices(BackgroundPanel, ForegroundPanel);
    }
}
```

`UpdatePanelIndices` keeps the two panels at `Index + 1` and `Index + 2` so they always render in the right order.

### `BorderRadius` propagation

```csharp
[NamedSetting("border-radius")]
public LiteralOrComputable BorderRadius {
    get;
    set {
        UpdateSetDirty(ref field, value);
        BackgroundPanel.BorderRadius = value;
        ForegroundPanel.BorderRadius = value;
    }
}
```

A single `border-radius: 4;` in the stylesheet writes to all three places. Same trick is used in `DoLayout`:

```csharp
protected override void DoLayout() {
    var x = (int)Computed.AbsoluteX;
    var y = (int)Computed.AbsoluteY;
    Viewport = (x, y, x + (int)Computed.Width, y + (int)Computed.Height);

    BackgroundPanel.Width  = new LiteralOrComputable(100, true);
    BackgroundPanel.Height = new LiteralOrComputable(100, true);
    ForegroundPanel.Width  = new LiteralOrComputable(MathF.Min(Progress * 100, 100), true);
    ForegroundPanel.Height = new LiteralOrComputable(100, true);

    BackgroundPanel.BorderRadius = BorderRadius;
    ForegroundPanel.BorderRadius = BorderRadius;

    BackgroundPanel.Layout();
    ForegroundPanel.Layout();
}
```

The width is clamped to `[0, 100]` via `MathF.Min(Progress * 100, 100)` — passing `Progress = 1.5` doesn't overflow the bar. Negative progress would still produce a 0-width foreground because `0% × X = 0`.

### `Progress` change detection

```csharp
[NamedSetting("progress")]
public float Progress {
    get;
    set {
        if (Math.Abs(field - value) < 0.001f) return;
        UpdateSetDirty(ref field, value);
    }
}
```

Sub-millipoint changes are dropped. This is the rounding floor — `Progress = 0.5001` won't trigger a layout if the previous value was `0.5`. Saves the per-frame relayout when an animation is interpolating very close to the same value.

## Style swap — the `ApplyStyleValue` override

Because `BackgroundPanel` and `ForegroundPanel` are typed `Panel` (not `Renderable`), `Panel.ApplyStyleValue` doesn't apply. `ProgressBar` overrides it to handle the `Renderable → Panel` translation:

```csharp
protected override void ApplyStyleValue(StyleSheet styleSheet, IStyleValue? styleValue, PropertyInfo propertyInfo) {
    if (styleValue is null) return;

    var oldValue = propertyInfo.GetValue(this) as Panel;
    Panel newPanel;
    switch (styleValue) {
        case GradientValue gv when propertyInfo.PropertyType == typeof(Panel):
            newPanel = new Panel(Context) {
                Background = gv.GenerateGradientPlane(),
                BorderRadius = BorderRadius,
                Width = new LiteralOrComputable(100, true),
                Height = new LiteralOrComputable(100, true),
                Parent = this
            };
            break;

        case ColorValue cv when propertyInfo.PropertyType == typeof(Panel):
            newPanel = new Panel(Context) {
                Background = new ColoredPlane { Color = cv.Vector },
                BorderRadius = BorderRadius,
                Width = new LiteralOrComputable(100, true),
                Height = new LiteralOrComputable(100, true),
                Parent = this
            };
            break;

        default:
            base.ApplyStyleValue(styleSheet, styleValue, propertyInfo);
            return;
    }

    HandleRenderableSwap(oldValue?.Background, newPanel.Background, propertyInfo.Name);
    propertyInfo.SetValue(this, newPanel);
}
```

`ColorValue` and `GradientValue` get wrapped in a fresh `Panel` with the right defaults (100% size, parented, border-radius'd). Anything else falls through to `UIElement.ApplyStyleValue`.

The `HandleRenderableSwap` call is what dequeues the old panel's background plane from the render queue and queues the new one — so a swap mid-frame doesn't leak a renderable in the queue.

## Hit testing — only the background

```csharp
public override void Test(MouseState mouse, Vector2 scale) {
    if (!Visible) return;
    base.Test(mouse, scale);
    BackgroundPanel.Test(mouse, scale);
    // ForegroundPanel intentionally not tested
}
```

Hovering over the bar tests the bar itself plus its background panel — but **not** the foreground. Reason: the foreground is decorative; you don't want a hover style to flicker as the progress moves and the foreground edge sweeps under the cursor. Either the bar as a whole is hovered, or the background is — never the foreground.

## Lifecycle plumbing

The remaining overrides are propagation of base-class hooks to both children:

```csharp
public override void StopRendering()        { BackgroundPanel.StopRendering(); ForegroundPanel.StopRendering(); }
public override void ApplyStyleSheet(s)     { base.ApplyStyleSheet(s); BackgroundPanel.ApplyStyleSheet(s); ForegroundPanel.ApplyStyleSheet(s); }
public override void Update(c)              { base.Update(c); BackgroundPanel.Update(c); ForegroundPanel.Update(c); }
public override void InvalidateLayout()     { if (NeedsLayout) return; base.InvalidateLayout(); BackgroundPanel.InvalidateLayout(); ForegroundPanel.InvalidateLayout(); }
public override void InvalidateCoordinates() { base.InvalidateCoordinates(); BackgroundPanel.InvalidateCoordinates(); ForegroundPanel.InvalidateCoordinates(); }
```

Pattern: every base hook that should recurse into children gets a one-liner override. `ProgressBar` doesn't put the children in a `List<UIElement>`, so it doesn't get the auto-recursion that [`Panel`](Panels.md#panel) gets — hence the manual forwarding.

The `if (NeedsLayout) return;` guard in `InvalidateLayout` is an early-out — already-dirty panels don't need to be marked dirty again or have their children re-marked.

## Why not just use a Panel with two children?

You could replicate `ProgressBar`'s structure with a `Panel` containing two child `Panel`s. The reason `ProgressBar` is its own class:

1. **`Progress` is a single, animatable value** — typing `progressBar.Progress = 0.5` is the natural API; manually sizing a child panel to `width: 50%` is not.
2. **The [markup parser](../Markup/Markup.md) needs a tag** — `<progress>` reads better than `<panel><panel/><panel/></panel>`.
3. **Stylesheet semantics** — `progress { ... }` selectors target the bar uniformly; targeting "the second child of the panel that's a progress bar" would be brittle.
4. **Hit-testing nuance** — the foreground-not-tested rule above — needs custom code.

## Related

- [Panel](Panels.md) — the building block.
- [NamedSetting](Abstractions.md#style-application-the-namedsetting-flow) — how `progress: 0.5;` becomes a property write.
- [Style DSL](../Style%20DSL/Style%20DSL.md) — `GradientValue`, `ColorValue`, animation interpolation.
- The [markup loader](../Markup/Markup.md) is what parses `<progress progress="0.5"/>` into one of these.
