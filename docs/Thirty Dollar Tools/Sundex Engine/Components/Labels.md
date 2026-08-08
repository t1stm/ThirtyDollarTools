# Labels

`Label` renders text. `Button` is a clickable label-in-a-flex-panel. `DropDownLabel` is a label-with-collapsible-panel.

> Source: `Sundex/Sundex.Components/Labels/`.

## `Label`

The simplest text component. Wraps a [`TextSlice`](../Engine/Text%20Rendering/Text%20Rendering.md#textslice) from a [`TextBuffer`](../Engine/Text%20Rendering/Text%20Rendering.md#textbuffer) and binds it to a UI position.

```csharp
[PreloadGraphicsContext]
public class Label : UIElement
{
    private const float ReferenceFontSize = 14;

    protected readonly TextBuffer? TextBuffer;

    public Label(UIContext context, ReadOnlySpan<char> text) : base(context) {
        TextBuffer = new TextBuffer(context.TextProvider, context.DeleteQueue);
        TextSlice  = TextBuffer.GetTextSlice(text);
    }

    protected TextSlice? TextSlice {
        get;
        set {
            field = value;
            if (field == null) return;
            Width  = field.Scale.X;
            Height = field.Scale.Y;
        }
    }

    [NamedSetting("text-value")]
    public ReadOnlySpan<char> Value {
        get => TextSlice != null ? TextSlice.Value : "";
        set => SetTextContents(value);
    }
    [NamedSetting("font-size")]  public LiteralOrComputable FontSizePx { get; set; }
    [NamedSetting("font-color")] public Vector4 Color { get; set; } = Vector4.One;

    public override string Tag => "label";
}
```

There's no backing `_textValue` field — `Value`'s getter reads `TextSlice.Value` directly, and its setter is just a thin wrapper over `SetTextContents`. The `TextSlice` property setter is where auto-sizing actually happens: assigning a new slice immediately re-reads `Width`/`Height` from its `Scale`.

### One TextBuffer per Label

A naïve implementation might share a single `TextBuffer` across all labels. Sundex doesn't — every label gets its own. Trade-off:

- **Pro**: each label is independent — disposing a label disposes its buffer; resizing a label's text doesn't perturb other labels' offsets.
- **Pro**: mutation is local — `slice.Value = "new text"` only touches one buffer.
- **Con**: many labels = many `glDrawElementsInstanced` calls (one per buffer), plus per-buffer GPU memory overhead.

For the Visualizer's UI density (~10-100 labels at a time), the per-label overhead is fine. Heavier text-density UIs (e.g. a log viewer) would benefit from a shared buffer with per-label slices.

### Font size resolution

```csharp
[NamedSetting("font-size")]
public LiteralOrComputable FontSizePx {
    get;
    set {
        if (field.IsPercentage == value.IsPercentage && Math.Abs(field.Value - value.Value) < 0.01f) return;
        field = value;
        if (TextSlice == null) return;
        TextSlice.FontSize = value.Resolve(ReferenceFontSize);

        var scale = TextSlice.Scale;
        Width  = scale.X;
        Height = scale.Y;
    }
}
```

`FontSizePx` is a `LiteralOrComputable` — `font-size: 100%;` resolves to `100% × 14 = 14px`. The `ReferenceFontSize = 14` is the engine-wide default; a literal `font-size: 24` is just 24px (the percentage flag stays false).

After updating the slice, `Width`/`Height` are set to the resolved text bounds — that's how a label auto-sizes around its text.

### `SetTextContents` — change-the-string semantics

```csharp
public void SetTextContents(ReadOnlySpan<char> text) {
    if (TextSlice == null) return;
    if (TextBuffer == null) return;

    if (text.Length == TextSlice.Value.Length && text.SequenceEqual(TextSlice.Value))
        return;

    if (text.Length > TextSlice.Length) {
        var position = TextSlice.Position;         // preserve — see note below
        TextSlice.Dispose();                        // free old slot
        var newSlice = TextBuffer.GetTextSlice(text);
        newSlice.UpdateManually = true;             // batch the property writes
        newSlice.Position  = position;
        newSlice.FontSize  = FontSizePx.Resolve(ReferenceFontSize);
        newSlice.Color     = Color;
        newSlice.UpdateManually = false;
        newSlice.UpdateCharacters();
        TextSlice = newSlice;                       // setter reads Scale — already correct
    } else {
        TextSlice.UpdateManually = true;
        TextSlice.FontSize = FontSizePx.Resolve(ReferenceFontSize);
        TextSlice.Color    = Color;
        TextSlice.Value    = text;
        TextSlice.UpdateManually = false;
        TextSlice.UpdateCharacters();
    }

    var scale = TextSlice.Scale;
    Width  = scale.X;
    Height = scale.Y;
    Layout();
}
```

An early-return dedups no-op calls: same length and same content skips the rest entirely. Then two paths based on whether the new text fits:

- **Same-length-or-shorter**: reuse the existing slice. Set `UpdateManually = true` to batch writes, set the properties, then re-trigger `UpdateCharacters` once. Avoids N redundant rebuilds during the batch.
- **Longer**: `Dispose()` the old slice (returns its slots to `_freeRanges`), allocate a new one, batch-write properties — **and explicitly restore `Position` before the manual `UpdateCharacters()` call**. `GetTextSlice`'s constructor already ran an initial `UpdateCharacters` once, at the default `(0,0,0)` position, before `Position`/`FontSize`/`Color` are applied here — without the explicit re-position-then-update, that stale, wrongly-placed render would be all that's ever written, since nothing else is guaranteed to touch `Position` again this frame.

The shorter path is the common case for UI labels — counters, progress text, status messages all rewrite to the same length or shorter. The dispose-and-replace path is the fallback for genuine growth.

### `DoLayout`

```csharp
protected override void DoLayout() {
    TextSlice?.SetPosition((Computed.AbsoluteX, Computed.AbsoluteY, 0));
}
```

That's the entire layout pass — push the slice's GPU position, that's it. The `TextSlice` internally updates every per-character `Position` in its range.

### `DrawSelf` / `StopRendering`

```csharp
protected override void DrawSelf(UIContext context) {
    if (TextBuffer != null) context.QueueRender(TextBuffer, Index);
}
public override void StopRendering() {
    if (TextBuffer != null) Context.DequeueRender(TextBuffer, Index);
}
```

The whole `TextBuffer` is queued at the label's `Index`. Every character in that buffer renders in one instanced draw call.

## `Button`

```csharp
public class Button : FlexPanel
{
    public Button(UIContext context, string label, Renderable? background = null)
        : this(context, new Label(context, label) {
            AnchorX = Anchor.Center, AnchorY = Anchor.Center
        }, background) { }

    public Button(UIContext context, Label label, Renderable? background = null) : base(context) {
        HorizontalAlign = Align.Center;
        VerticalAlign   = Align.Center;
        Background      = background;
        Children        = [Label = label];
        UpdateCursorOnHover = true;
    }

    public override LayoutDirection Direction { get; set; } = LayoutDirection.Vertical;
    public override float Padding { get; set; } = 5;

    public override string Tag => "button";
    public Label Label { get; set; }

    [NamedSetting("text-value")] public ReadOnlySpan<char> Value { get => Label.Value; set => Label.Value = value; }
    [NamedSetting("font-size")]  public LiteralOrComputable FontSizePx { get => Label.FontSizePx; set => Label.FontSizePx = value; }
    [NamedSetting("width")]      public override LiteralOrComputable Width { get; set; } = new(0, false, true);
}
```

A `Button` is a [`FlexPanel`](Panels.md#flexpanel) with one `Label` child, both alignments centred, padding 5, vertical layout direction, cursor pointer on hover.

The `[NamedSetting]` properties on Button are **forwarders**: `button.Value = "Save"` writes through to `button.Label.Value`. This is what lets stylesheet selectors target buttons directly:

```css
button { font-size: 14; }
button:hover { font-color: rgb(255 255 0); }
```

…without having to navigate into the child label.

### `Width = AutoSize`

```csharp
[NamedSetting("width")]
public override LiteralOrComputable Width { get; set; } = new(0, false, true);
```

Buttons shrink-wrap to their label by default. Override to `100` or `Percent(50)` if you want a fixed-size button.

`Padding = 5` ensures there's at least 5px of breathing room around the label inside the button — without it, the label would render at the exact button bounds.

### Click handling

`Button` inherits `OnClick` from `UIElement`. Standard usage:

```csharp
new Button(context, "Save") {
    OnClick = btn => Save()
}
```

The release-inside semantics from [`UIContext.UpdatePointer`](Abstractions.md#hit-testing-testmousestate-vector2-scale-and-uicontextupdatepointer) applies: pressing-then-dragging-off cancels the click.

## `DropDownLabel`

A label that, when clicked, toggles the visibility of an attached panel below it.

```csharp
public sealed class DropDownLabel : Panel
{
    public DropDownLabel(UIContext context, string text, List<UIElement> panelChildren, bool hoverChildren = true)
        : base(context)
    {
        if (hoverChildren)
            panelChildren.ForEach(child => child.UpdateCursorOnHover = true);

        Panel = new FlexPanel(context) {
            Parent = this,
            Children = panelChildren,
            Direction = LayoutDirection.Vertical,
            Visible = false,                              // hidden by default
            Background = new ColoredPlane { Color = (0.2f, 0.2f, 0.2f, 1f) },
            Spacing = 4,
            Padding = 4,
            Width  = LiteralOrComputable.AutoSize,
            Height = LiteralOrComputable.AutoSize
        };

        Label = new Label(context, text) {
            Parent = this,
            UpdateCursorOnHover = true,
            OnClick = _ => { Panel.Visible = !Panel.Visible; }   // toggle
        };

        Children = [Label, Panel];
    }

    public FlexPanel Panel { get; }
    public Label     Label { get; }
}
```

Layout:

```
[Label]                ← always visible
[Panel]                ← below the label, hidden until clicked
  ├── child 1
  ├── child 2
  └── ...
```

### `DoLayout` — position the panel below the label

```csharp
protected override void DoLayout() {
    Label.Layout();
    Panel.Y = Computed.Height + 10;       // 10px below
    Panel.Layout();
}
public override (float w, float h) Measure(float pw, float ph) {
    return Label.Measure(pw, ph);          // size = label size only
}
public override LiteralOrComputable Width  => Label.Width;
public override LiteralOrComputable Height => Label.Height;
```

The drop-down's *measured size* is the label's size. The panel floats below it and overflows the parent — important: nothing scissors it. If the label is near the bottom of the window, the panel renders off-screen.

### Click-outside dismissal

```csharp
public override void Test(MouseState mouse, Vector2 scale) {
    var hide_panel = mouse.IsButtonPressed(MouseButton.Left);
    Label.Test(mouse, scale);
    Panel.Test(mouse, scale);
    if (hide_panel && !Label.IsHovered)
        Panel.Visible = false;
}
```

If the user clicks (button just-pressed this frame) and the click isn't on the label, hide the panel. Note the `!Label.IsHovered` check — clicking the label is what *toggles* visibility, so this branch must skip toggle clicks.

Subtle: clicks on the panel *children* will also hide the panel (since `Label.IsHovered` is false). That's typically what you want for a menu — pick an option, menu closes — but it means click handlers on panel children fire **before** the panel is hidden, in the same frame.

### `DrawSelf` is empty

```csharp
protected override void DrawSelf(UIContext context) { }
```

`DropDownLabel` itself has no background. The label and the panel render their own backgrounds; the drop-down is purely a layout/state coordinator.

## Threading

`Label.SetTextContents` mutates GPU buffers (via `TextSlice`) and must run on the GL thread. `Button.OnClick` fires from `Test`, which runs on the main loop — safe by construction.

For off-thread label updates (e.g. polling a backend), round-trip via `Game.Enqueue`:

```csharp
ThreadRunner.RunTask(() => {
    var status = FetchStatus();
    Game.Enqueue(_ => statusLabel.Value = status);
});
```

See [Threading](../Engine/Threading.md) for the canonical pattern.

## Related

- [Text Rendering](../Engine/Text%20Rendering/Text%20Rendering.md) — what `Label` is built on top of.
- [FlexPanel](Panels.md#flexpanel) — `Button`'s base class.
- [UIElement](Abstractions.md) — `Label` extends this directly (not via `Panel`).
- The Visualizer's markup files use `<button>`, `<label>`, and `<dropdown>` extensively.
