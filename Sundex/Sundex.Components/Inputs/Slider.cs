using OpenTK.Mathematics;
using Sundex.Components.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Components.Bars;
using Sundex.Components.Panels;
using Sundex.Style.DSL;

namespace Sundex.Components.Inputs;

/// <summary>
///     Horizontal slider: a <see cref="ProgressBar" /> whose fill tracks a value in
///     [<see cref="Min" />, <see cref="Max" />], set by pressing/dragging anywhere on the bar
///     (capture keeps the drag alive outside the bounds). Optional <see cref="Step" /> snapping.
///     A media bar's extras are settings: a <see cref="SecondaryPanel" /> fill under the value (what
///     has buffered), a <see cref="ThumbPanel" /> dot that follows the pointer while hovered and the
///     value while dragged, the wheel, and a hit area taller than the bar.
/// </summary>
[MarkupTag("slider")]
public class Slider : ProgressBar
{
    private float _thumbAt = -1;

    public Slider(UIContext context, Renderable? background = null, Renderable? foreground = null)
        : base(context,
            background ?? new ColoredPlane { Color = new Vector4(1, 1, 1, 0.15f) },
            foreground ?? new ColoredPlane { Color = new Vector4(1, 1, 1, 0.6f) })
    {
        UpdateCursorOnHover = true;
        SecondaryPanel = NewPart(new Vector4(0, 0, 0, 0));
        ThumbPanel = NewPart(new Vector4(1, 1, 1, 1));
    }

    public override string Tag => "slider";

    public double Min { get; set; }
    public double Max { get; set; } = 1;

    /// <summary>Snap increment; null (default) means continuous.</summary>
    public double? Step { get; set; }

    /// <summary>Fires on every change of <see cref="Value" />, the code's own writes included.</summary>
    public Action<Slider>? OnValueChanged { get; set; }

    /// <summary>
    ///     Fires only when the user moves the value: a press, a drag, the wheel. A bar that also
    ///     follows something (a playback position) listens here, so following it seeks nothing.
    /// </summary>
    public Action<Slider>? OnInput { get; set; }

    public double Value
    {
        get;
        set
        {
            var clamped = Math.Clamp(Snap(value), Min, Max);
            if (field.Equals(clamped)) return;
            field = clamped;
            Progress = Max > Min ? (float)((clamped - Min) / (Max - Min)) : 0;
            if (ThumbShown && IsPressed) InvalidateLayout();
            OnValueChanged?.Invoke(this);
        }
    }

    /// <summary>A second fill under the value's, 0 to 1 of the bar: how much of a track has loaded.</summary>
    [NamedSetting("secondary-progress")]
    public float SecondaryProgress
    {
        get;
        set
        {
            value = Math.Clamp(value, 0, 1);
            if (Math.Abs(field - value) < 0.001f) return;
            UpdateSetDirty(ref field, value);
        }
    }

    [NamedSetting("secondary")]
    public Panel SecondaryPanel
    {
        get;
        set => SwapPart(ref field, value);
    }

    /// <summary>The dot, shown while the pointer is on the bar. 0 (the default) shows none.</summary>
    [NamedSetting("thumb-size")]
    public float ThumbSize
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    [NamedSetting("thumb")]
    public Panel ThumbPanel
    {
        get;
        set => SwapPart(ref field, value);
    }

    /// <summary>How far one wheel notch moves the value; 0 (the default) leaves the wheel to what's behind.</summary>
    [NamedSetting("wheel-step")]
    public double WheelStep { get; set; }

    /// <summary>Extra height above and below the bar that still takes the pointer: a thin bar is hard to hit.</summary>
    [NamedSetting("hit-slop")]
    public float HitSlop { get; set; }

    public override int Index
    {
        get => base.Index;
        internal set
        {
            base.Index = value;
            // ProgressBar numbers its panels when parented; a depth that changes later (a z-index
            // above it) has to reach them too.
            BackgroundPanel?.Index = value + 1;
            SecondaryPanel?.Index = value + 2;
            ForegroundPanel?.Index = value + 3;
            ThumbPanel?.Index = value + 4;
        }
    }

    public override UIElement? Parent
    {
        get => base.Parent;
        set
        {
            base.Parent = value;
            SecondaryPanel.Parent = this;
            ThumbPanel.Parent = this;
            Index = Index;
        }
    }

    private Panel NewPart(Vector4 color) => new(Context)
    {
        Background = new ColoredPlane { Color = color },
        Parent = this
    };

    private void SwapPart(ref Panel field, Panel value)
    {
        var old = field;
        UpdateSetDirty(ref field, value);
        field.Parent = this;
        Index = Index;
        if (!Drawn || ReferenceEquals(old, value)) return;
        old?.StopRendering();
        if (ReferenceEquals(value, ThumbPanel) && !ThumbShown) return;
        value.DrawTo(Context);
    }

    private bool ThumbShown => ThumbSize > 0 && (IsHovered || IsPressed) && !Disabled;

    public override bool HandlePress(float x, float y)
    {
        SetFromPointer(x);
        return true;
    }

    public override void HandlePointerDrag(float x, float y)
    {
        SetFromPointer(x);
    }

    public override bool HandleScroll(Vector2 scrollDelta)
    {
        if (WheelStep <= 0 || Disabled) return false;
        var before = Value;
        Value += Math.Sign(scrollDelta.Y) * WheelStep;
        if (!before.Equals(Value)) OnInput?.Invoke(this);
        return true;
    }

    public override UIElement? HitTest(float x, float y)
    {
        if (!Visible || PointerEvents == PointerEvents.None) return null;
        var box = Computed;
        return x >= box.AbsoluteX && x <= box.AbsoluteX + box.Width &&
               y >= box.AbsoluteY - HitSlop && y <= box.AbsoluteY + box.Height + HitSlop
            ? this
            : null;
    }

    private void SetFromPointer(float x)
    {
        var width = Computed.Width;
        if (width <= 0) return;

        var fraction = Math.Clamp((x - Computed.AbsoluteX) / width, 0, 1);
        var before = Value;
        Value = Min + fraction * (Max - Min);
        if (!before.Equals(Value)) OnInput?.Invoke(this);
    }

    private double Snap(double value)
    {
        if (Step is not { } step || step <= 0) return value;
        // ponytail: rounding kills binary float noise from the division/multiplication.
        return Math.Round(Math.Round(value / step) * step, 9);
    }

    // ── The extra parts ──────────────────────────────────────────────────────────────────────

    protected override void HoverChanged(bool hovered)
    {
        base.HoverChanged(hovered);
        if (ThumbSize <= 0) return;
        // The dot follows the pointer, which moves without telling anybody: watch it while on.
        if (hovered) Context.RegisterUpdate(this);
        else Context.UnregisterUpdate(this);
        InvalidateLayout();
        if (!Drawn) return;
        if (ThumbShown) ThumbPanel.DrawTo(Context);
        else ThumbPanel.StopRendering();
    }

    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);
        if (!ThumbShown || IsPressed) return;
        if (Math.Abs(ThumbCentre() - _thumbAt) >= 0.5f) InvalidateLayout();
    }

    /// <summary>Where the dot's centre goes, from the bar's left edge: the value while dragged, the pointer otherwise.</summary>
    private float ThumbCentre()
    {
        var width = Computed.Width;
        return IsPressed ? Progress * width : Math.Clamp(Context.PointerX - Computed.AbsoluteX, 0, width);
    }

    protected override void DrawSelf(UIContext context)
    {
        BackgroundPanel.DrawTo(context);
        SecondaryPanel.DrawTo(context);
        ForegroundPanel.DrawTo(context);
        if (ThumbShown) ThumbPanel.DrawTo(context);
    }

    protected override void DoLayout()
    {
        base.DoLayout();
        SecondaryPanel.Width = new LiteralOrComputable(MathF.Min(SecondaryProgress * 100, 100), true);
        SecondaryPanel.Height = new LiteralOrComputable(100, true);
        SecondaryPanel.BorderRadius = BorderRadius;
        SecondaryPanel.Layout();

        if (ThumbSize <= 0) return;
        _thumbAt = ThumbCentre();
        ThumbPanel.Width = ThumbSize;
        ThumbPanel.Height = ThumbSize;
        ThumbPanel.BorderRadius = ThumbSize / 2;
        ThumbPanel.X = _thumbAt - ThumbSize / 2;
        ThumbPanel.Y = (Computed.Height - ThumbSize) / 2;
        ThumbPanel.InvalidateCoordinates();
        ThumbPanel.Layout();
    }

    public override void StopRendering()
    {
        base.StopRendering();
        SecondaryPanel.StopRendering();
        ThumbPanel.StopRendering();
    }

    public override void ApplyStyleSheet(StyleSheet styleSheet)
    {
        base.ApplyStyleSheet(styleSheet);
        SecondaryPanel.ApplyStyleSheet(styleSheet);
        ThumbPanel.ApplyStyleSheet(styleSheet);
    }

    public override void InvalidateLayout()
    {
        if (NeedsLayout) return;
        base.InvalidateLayout();
        SecondaryPanel.InvalidateLayout();
        ThumbPanel.InvalidateLayout();
    }

    public override void InvalidateCoordinates()
    {
        base.InvalidateCoordinates();
        SecondaryPanel.InvalidateCoordinates();
        ThumbPanel.InvalidateCoordinates();
    }

    public override void ApplyClip(Vector4i? clip)
    {
        base.ApplyClip(clip);
        SecondaryPanel.ApplyClip(clip);
        ThumbPanel.ApplyClip(clip);
    }

    protected override void OnDispose()
    {
        Context.UnregisterUpdate(this);
        base.OnDispose();
    }
}
