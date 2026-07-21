using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;

namespace Sundex.Components.Scroll;

/// <summary>
///     Vertical scroll container: stacks its children top-to-bottom, offsets them by
///     <see cref="ScrollY" />, clips them to its bounds (scissor via <see cref="UIElement.ApplyClip" />),
///     handles wheel input, and hosts a draggable <see cref="ScrollBar" /> at its right edge.
///     The bar overlays the content's right edge and stays visible when content fits
///     (full-height thumb) — hiding queued renderables isn't supported by the framework yet.
/// </summary>
public class ScrollView : Panel
{
    // Breathing room on both sides: between the rightmost content and the bar
    // itself (on top of the bar's own width), mirrored on the left so content
    // isn't flush against one edge and gapped from the other.
    private const float ScrollBarGutter = 6f;

    private readonly ScrollBar _bar;
    private float[] _heightsCache = [];
    private Vector4i? _inheritedClip;
    private float _scrollY;

    public ScrollView(UIContext context) : base(context)
    {
        Direction = LayoutDirection.Vertical;
        _bar = new ScrollBar(context, this)
        {
            OnScrolled = bar => ScrollY = bar.Percentage * MaxScroll
        };
    }

    public override string Tag => "scroll-view";

    /// <summary>Total stacked height of the children, computed during layout.</summary>
    public float ContentHeight { get; private set; }

    /// <summary>Maximum scroll offset; 0 when the content fits.</summary>
    public float MaxScroll { get; private set; }

    /// <summary>Pixels scrolled per wheel notch.</summary>
    public float WheelSpeed { get; set; } = 48;

    public float ScrollY
    {
        get => _scrollY;
        set
        {
            var v = Math.Clamp(value, 0, MaxScroll);
            if (Math.Abs(_scrollY - v) < 0.0001f) return;
            _scrollY = v;
            InvalidateLayout();
        }
    }

    public override bool HandleScroll(Vector2 scrollDelta)
    {
        if (MaxScroll <= 0) return false;
        ScrollY -= scrollDelta.Y * WheelSpeed;
        return true;
    }

    protected override void DoLayout()
    {
        var innerWidth = Math.Max(0, Computed.Width - 2 * Padding - _bar.Width.Value - 2 * ScrollBarGutter);
        var innerHeight = Math.Max(0, Computed.Height - 2 * Padding);

        // Measure once, then stack with the scroll offset applied.
        // Reused across layout passes — this runs every scroll frame.
        if (_heightsCache.Length < Children.Count)
            _heightsCache = new float[Children.Count];
        var heights = _heightsCache;
        ContentHeight = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            heights[i] = Children[i].Measure(innerWidth, innerHeight).height;
            ContentHeight += heights[i] + (i > 0 ? Spacing : 0);
        }

        MaxScroll = Math.Max(0, ContentHeight - innerHeight);
        _scrollY = Math.Clamp(_scrollY, 0, MaxScroll);

        var offset = -_scrollY;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.X = ScrollBarGutter;
            child.Y = offset;
            // Percent-width rows fill this reserved (bar-inset) width instead of the
            // full box, so they don't render underneath the bar. Fixed-width children
            // (e.g. a centered dialog element) are left to their own declared width.
            child.ParentAssignedWidth = child.Width.IsPercentage ? innerWidth : null;
            offset += heights[i] + Spacing;
        }

        base.DoLayout(); // background + children layout

        _bar.Index = Index + 2; // above content rows for hit-testing/render order
        _bar.ScrollBlock.Height = LiteralOrComputable.Percent(
            ContentHeight > 0 ? Math.Clamp(100f * innerHeight / ContentHeight, 10, 100) : 100);
        _bar.SetPercentage(MaxScroll > 0 ? _scrollY / MaxScroll : 0);
        _bar.Layout();

        ApplyClip(_inheritedClip);
    }

    /// <summary>Clips the content children to this view's bounds, intersected with any outer clip.</summary>
    public override void ApplyClip(Vector4i? clip)
    {
        _inheritedClip = clip;
        var x = (int)Computed.AbsoluteX;
        var y = (int)Computed.AbsoluteY;
        var own = IntersectClip(new Vector4i(x, y, x + (int)Computed.Width, y + (int)Computed.Height), clip);

        foreach (var child in Children) child.ApplyClip(own);
        if (Background != null) Background.ClipRect = clip;
        _bar.ApplyClip(clip);
    }

    public override UIElement? HitTest(float x, float y)
    {
        // Content is clipped to the bounds, so hits outside them must not reach the
        // (visually hidden) overflowing children.
        if (!Visible || !ContainsPoint(x, y)) return null;
        var best = base.HitTest(x, y);
        var bar = _bar.HitTest(x, y);
        return bar != null && (best == null || bar.Index >= best.Index) ? bar : best;
    }

    public override void DrawTo(UIContext ctx)
    {
        if (!Visible) return;
        base.DrawTo(ctx);
        _bar.DrawTo(ctx);
    }

    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);
        _bar.Update(uiContext);
    }

    public override void StopRendering()
    {
        base.StopRendering();
        _bar.StopRendering();
    }

    public override void InvalidateLayout()
    {
        base.InvalidateLayout();
        _bar?.InvalidateLayout();
    }

    public override void InvalidateCoordinates()
    {
        base.InvalidateCoordinates();
        _bar?.InvalidateCoordinates();
    }
}
