# Components

`Sundex.Components` is the **UI tree library** — a small set of `UIElement` subclasses that draw text, panels, buttons, scroll bars, and a file selector on top of the [[../Engine/Engine|engine]] primitives. Every visible thing the application shows is some composition of these classes.

> Source: `Sundex/Sundex.Components/`.

## What's in here

```
Components/
├── Abstractions/      ← UIElement, UIContext, enums, ComputedRectangle, LiteralOrComputable
├── Attributes/        ← [NamedSetting], [RenderPriority]
├── Bars/              ← ProgressBar
├── Labels/            ← Label, Button, DropDownLabel
├── Panels/            ← Panel, FlexPanel, StackPanel, WindowFrame
├── Scroll/            ← ScrollBar
└── File Selector/     ← FileSelection
```

The split is deliberate:

- **[[Abstractions|Abstractions]]** is the contract every component implements: lifecycle, layout, hit testing, style application.
- **[[Bars|Bars]]**, **[[Labels|Labels]]**, **[[Panels|Panels]]** are concrete components that compose into trees.
- **Scroll** and **File Selector** are domain-specific composites built from the primitives — small enough to live alongside, big enough to merit their own folders.

## Mental model

```
                   UIElement ◄────────── abstract base
                   ┌────┴────┬─────────┬──────────┐
                   ▼         ▼         ▼          ▼
                 Label     Panel    ScrollBar  ProgressBar
                                    │
                          ┌─────────┼─────────┐
                          ▼         ▼         ▼
                        FlexPanel StackPanel WindowFrame
                          │
                       Button (FlexPanel + Label)
                       DropDownLabel (Panel + FlexPanel + Label)
                       FileSelection (composite of all of the above)
```

Every tree has exactly one [[Abstractions#UIContext|`UIContext`]] at the top, shared by every element. The context is what owns the layered render queue, the [[../Engine/Text Rendering/Text Rendering|`TextProvider`]], the [[../Engine/Asset Management|`AssetProvider`]], and the cursor request callback.

## How a frame flows through the tree

1. **Mouse / Keyboard input** — `Scene.Mouse(...)` calls `root.Test(mouse, scale)`, which recurses to children. Each element updates its `IsHovered` / `IsPressed` flags and fires `OnClick` callbacks.
2. **Layout** — if anything called `InvalidateLayout()`, the next `root.Layout()` rewalks the tree. Each element gets a chance to `Measure(parentW, parentH)` (returns its desired size for `Auto` parents) and then `DoLayout()` (positions itself + children).
3. **Style updates** — animations registered via `UIContext.RegisterUpdate` run their per-frame `Update()` and may mutate background colours, positions, etc.
4. **Render** — `UIContext.Render()` iterates the **layered render queue** and calls `IRenderable.Render(camera)` on each entry. Order = element `Index` → render priority within element.

The render queue is **not** a tree walk. The tree walk is `DrawTo`, which is what *populates* the queue. This separation is what lets a Label nested 8 deep render at index 17 between two completely unrelated Panels — the queue is flat and ordered by `Index`.

## The `[NamedSetting]` / `[RenderPriority]` story

Two attributes drive most of the magic:

- **`[NamedSetting("foo")]`** on a property tells the [[../Style DSL/Style DSL|style DSL]] *"this property is settable from a stylesheet under the name `foo`"*. `UIElement.ApplyStyleSheet` reflects over the attribute, finds matching style values, and invokes the setter. This is how `width: 100%`, `font-color: white`, `padding: 5` all become C# property writes.
- **`[RenderPriority(N)]`** on a property tells the render queue what order to draw in within a single element. `Panel.Background` is `[RenderPriority(0)]` so it draws *before* (i.e. behind) any non-prioritised children.

See [[Abstractions#NamedSetting|Abstractions]] for the gory details.

## Where to start reading

If you're new to the codebase:

1. **[[Abstractions|Abstractions]]** — the `UIElement` contract. Everything else is variations on this theme.
2. **[[Panels|Panels]]** — `Panel` is the simplest concrete element; `FlexPanel` is the layout workhorse.
3. **[[Labels|Labels]]** — how text wires into the [[../Engine/Text Rendering/Text Rendering|MSDF text pipeline]].
4. **[[Bars|Bars]]** — `ProgressBar` is a good example of a composite that wraps two child Panels with controlled style propagation.

## Threading

UI mutation is **GL-thread only**. `Scene.Mouse`, `Scene.Update`, and `Scene.Render` all run on the main loop and serialise mutation against the render queue. Off-thread work (e.g. `FileSelection`'s `Task.Run(RefreshFiles)`) populates lists off-thread but commits the children-list assignment back into the tree under a `SemaphoreSlim` — strictly speaking still touching the tree off-thread, see [[#FileSelection threading|FileSelection threading]] for the caveat.

## Related

- [[../Engine/Engine|Engine]] — the lower layer this builds on.
- [[../Markup/Markup|Markup]] — the XML-ish DSL that constructs trees of these components from disk.
- [[../Style DSL/Style DSL|Style DSL]] — the stylesheet DSL that drives `[NamedSetting]` properties.
