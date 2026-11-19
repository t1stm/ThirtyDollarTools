using System.Reflection;
using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Components.Renderer.Planes;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Components.Panels;

/// <summary>
///     A CSS flex container. Children are laid out by their own settings (<c>grow</c>, <c>shrink</c>,
///     <c>basis</c>, <c>min-width</c>, <c>max-width</c>, margins, <c>align-self</c>,
///     <c>position: absolute</c> and insets, <c>aspect-ratio</c>) along this panel's direction, with
///     per-side padding, row and column gaps, <c>justify</c>, <c>align-items</c>, wrapping and
///     overflow that clips or scrolls on either axis. A <see cref="Region" /> child takes no space:
///     its children are laid out as this panel's own.
///     <para>
///         The older settings still work: <c>spacing</c> is both gaps, <c>padding</c> all four
///         sides, and <c>horizontal-align</c> / <c>vertical-align</c> stand in for <c>justify</c>
///         and <c>align-items</c> until those are set.
///     </para>
///     <para>
///         Unlike CSS, a percentage on the main axis is a share of the space the other children
///         leave, as it always was in Sundex: <c>height = 100%</c> under a fixed header is the rest
///         of the column. On the cross axis it is a share of the panel.
///     </para>
/// </summary>
[MarkupTag("flex")]
public class FlexPanel : Panel, IPositioningElement
{
    private const float ThumbWidth = 5;
    private const float ThumbMinLength = 24;

    /// <summary>
    ///     How far past the line a wrapped item may reach and still stay on it. A box sized to its
    ///     content sums its items in one order and is laid out by summing them in another, and the
    ///     float rounding between the two broke a line that fitted exactly.
    /// </summary>
    private const float WrapSlack = 0.01f;

    /// <summary>How far above its own depth a scroller draws its thumb.</summary>
    private const int ThumbDepth = 24;

    private readonly Dictionary<(float, float), (float Width, float Height)> _measures = [];
    private float _contentW, _contentH;
    private Vector4i? _inheritedClip;
    private bool _justifySet, _itemsSet;
    private float _scrollX, _scrollY;
    private BoxPlane? _thumb;
    private bool _thumbDrag;
    private float _thumbGrab;

    public FlexPanel(UIContext context) : base(context)
    {
        // Content-sized unless told otherwise, like a CSS box.
        Width = LiteralOrComputable.AutoSize;
        Height = LiteralOrComputable.AutoSize;
    }

    public override string Tag => "flex";

    #region Settings

    [NamedSetting("padding-top")]
    public float PaddingTop
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    [NamedSetting("padding-right")]
    public float PaddingRight
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    [NamedSetting("padding-bottom")]
    public float PaddingBottom
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    [NamedSetting("padding-left")]
    public float PaddingLeft
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    // Where content starts from each edge: the padding plus the border, which takes layout space
    // as a CSS border does, so a bordered box sized by its content is the border's width larger.
    protected float InsetTop => PaddingTop + BorderTop;
    protected float InsetRight => PaddingRight + BorderRight;
    protected float InsetBottom => PaddingBottom + BorderBottom;
    protected float InsetLeft => PaddingLeft + BorderLeft;

    /// <summary>All four sides at once.</summary>
    [NamedSetting("padding")]
    public override float Padding
    {
        get => PaddingTop;
        set => PaddingTop = PaddingRight = PaddingBottom = PaddingLeft = value;
    }

    // Children are placed with the padding already in their X and Y, so the offset the base
    // layout adds for a positioning parent's padding must be none.
    float IPositioningElement.Padding
    {
        get => 0;
        set => Padding = value;
    }

    /// <summary>The space between children on a row.</summary>
    [NamedSetting("column-gap")]
    public float ColumnGap
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    /// <summary>The space between children in a column, and between wrapped rows.</summary>
    [NamedSetting("row-gap")]
    public float RowGap
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    [NamedSetting("gap")]
    public float Gap
    {
        get => ColumnGap;
        set => ColumnGap = RowGap = value;
    }

    /// <summary>The older name for <see cref="Gap" />.</summary>
    [NamedSetting("spacing")]
    public override float Spacing
    {
        get => Gap;
        set => Gap = value;
    }

    [NamedSetting("justify")]
    public Justify Justify
    {
        get;
        set
        {
            _justifySet = true;
            UpdateSetDirty(ref field, value);
        }
    } = Justify.Start;

    /// <summary>
    ///     Where children sit on the cross axis. Unlike CSS, the default is <see cref="Align.Start" />:
    ///     children keep their own size unless this, or their <c>align-self</c>, says stretch.
    /// </summary>
    [NamedSetting("align-items")]
    public Align AlignItems
    {
        get;
        set
        {
            _itemsSet = true;
            UpdateSetDirty(ref field, value);
        }
    } = Align.Start;

    [NamedSetting("horizontal-align")]
    public Align HorizontalAlign
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = Align.Start;

    [NamedSetting("vertical-align")]
    public Align VerticalAlign
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = Align.Start;

    [NamedSetting("wrap")]
    public virtual bool Wrap
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    [NamedSetting("overflow-x")]
    public Overflow OverflowX
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    [NamedSetting("overflow-y")]
    public Overflow OverflowY
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    [NamedSetting("overflow")]
    public Overflow Overflow
    {
        get => OverflowY;
        set => OverflowX = OverflowY = value;
    }

    /// <summary>Whether a scrolling panel draws its thumb.</summary>
    [NamedSetting("scrollbar")]
    public bool ShowScrollbar { get; set; } = true;

    /// <summary>Requires Shift for horizontal wheel input and keeps that input in this panel while held.</summary>
    [NamedSetting("scroll-x-requires-shift")]
    public bool ScrollXRequiresShift { get; set; }

    [NamedSetting("scrollbar-color")]
    public Vector4 ScrollbarColor
    {
        get;
        set
        {
            field = value;
            _thumb?.Color = value;
        }
    } = new(1, 1, 1, 0.3f);

    /// <summary>Runs after each layout pass: how a page learns a row scrolled into reach.</summary>
    public Action? AfterLayout { get; set; }

    public bool ScrollsX => OverflowX == Overflow.Scroll;
    public bool ScrollsY => OverflowY == Overflow.Scroll;
    private bool Clips => OverflowX != Overflow.Visible || OverflowY != Overflow.Visible;

    public override bool CanCollapseX => base.CanCollapseX || Clips;
    public override bool CanCollapseY => base.CanCollapseY || Clips;

    #endregion

    #region Scrolling

    public float ScrollY
    {
        get => _scrollY;
        set
        {
            var clamped = Math.Clamp(value, 0, MaxScrollY);
            if (Math.Abs(_scrollY - clamped) < 0.01f) return;
            _scrollY = clamped;
            InvalidateLayout();
        }
    }

    public float ScrollX
    {
        get => _scrollX;
        set
        {
            var clamped = Math.Clamp(value, 0, MaxScrollX);
            if (Math.Abs(_scrollX - clamped) < 0.01f) return;
            _scrollX = clamped;
            InvalidateLayout();
        }
    }

    public float MaxScrollY => Math.Max(0, _contentH - InnerHeight);
    public float MaxScrollX => Math.Max(0, _contentW - InnerWidth);
    public float ContentWidth => _contentW;
    public float ContentHeight => _contentH;
    public float InnerWidth => Math.Max(0, Computed.Width - InsetLeft - InsetRight);
    public float InnerHeight => Math.Max(0, Computed.Height - InsetTop - InsetBottom);

    /// <summary>Scrolls so <paramref name="child" /> sits in the middle (<c>scrollIntoView({block: 'center'})</c>).</summary>
    public void CenterOn(UIElement child)
    {
        var y = child.Computed.AbsoluteY - Computed.AbsoluteY + _scrollY;
        ScrollY = y - InnerHeight / 2 + child.Computed.Height / 2;
    }

    /// <summary>Scrolls the least that brings <paramref name="child" /> fully into view.</summary>
    public void ScrollIntoView(UIElement child, float margin = 0)
    {
        var y = child.Computed.AbsoluteY - Computed.AbsoluteY + _scrollY;
        if (y - margin < _scrollY) ScrollY = y - margin;
        else if (y + child.Computed.Height + margin > _scrollY + InnerHeight)
            ScrollY = y + child.Computed.Height + margin - InnerHeight;
    }

    public override bool HandleScroll(Vector2 scrollDelta)
    {
        // Shift owns the gesture even at an edge or when all the content fits.
        if (ScrollsX && ScrollXRequiresShift && Context.ShiftDown)
        {
            ScrollX -= (scrollDelta.X != 0 ? scrollDelta.X : scrollDelta.Y) * 96;
            return true;
        }

        if (ScrollsY && MaxScrollY > 0)
        {
            var before = _scrollY;
            ScrollY -= scrollDelta.Y * 96;
            if (Math.Abs(before - _scrollY) > 0.01f) return true;
        }

        if (ScrollsX && MaxScrollX > 0 && (!ScrollXRequiresShift || Context.ShiftDown))
        {
            var before = _scrollX;
            ScrollX -= (scrollDelta.X != 0 ? scrollDelta.X : scrollDelta.Y) * 96;
            if (Math.Abs(before - _scrollX) > 0.01f) return true;
        }

        return false;
    }

    public override bool HandlePress(float x, float y)
    {
        if (!ThumbHit(x, y)) return false;
        _thumbDrag = true;
        if (ScrollsY && MaxScrollY > 0)
        {
            var (top, length) = ThumbRangeY();
            _thumbGrab = y - Computed.AbsoluteY - top;
            if (_thumbGrab < 0 || _thumbGrab > length) _thumbGrab = length / 2;
        }

        HandlePointerDrag(x, y);
        return true;
    }

    public override void HandlePointerDrag(float x, float y)
    {
        if (!_thumbDrag) return;
        if (!Context.PointerDown)
        {
            _thumbDrag = false;
            return;
        }

        if (ScrollsY && MaxScrollY > 0)
        {
            var (_, length) = ThumbRangeY();
            var travel = Computed.Height - length;
            if (travel > 0) ScrollY = (y - Computed.AbsoluteY - _thumbGrab) / travel * MaxScrollY;
        }
        else if (ScrollsX && MaxScrollX > 0)
        {
            var length = ThumbLengthX();
            var travel = Computed.Width - length;
            if (travel > 0) ScrollX = (x - Computed.AbsoluteX - length / 2) / travel * MaxScrollX;
        }
    }

    private bool ThumbHit(float x, float y)
    {
        if (!ShowScrollbar) return false;
        if (ScrollsY && MaxScrollY > 0)
            return x >= Computed.AbsoluteX + Computed.Width - ThumbWidth - 3 && x <= Computed.AbsoluteX + Computed.Width;
        if (ScrollsX && MaxScrollX > 0)
            return y >= Computed.AbsoluteY + Computed.Height - ThumbWidth - 3 && y <= Computed.AbsoluteY + Computed.Height;
        return false;
    }

    private (float Top, float Length) ThumbRangeY()
    {
        var view = Computed.Height;
        var content = _contentH + InsetTop + InsetBottom;
        var length = Math.Max(ThumbMinLength, view * view / Math.Max(content, 1));
        var top = MaxScrollY > 0 ? _scrollY / MaxScrollY * (view - length) : 0;
        return (top, length);
    }

    private float ThumbLengthX() =>
        Math.Max(ThumbMinLength, Computed.Width * Computed.Width / Math.Max(_contentW + InsetLeft + InsetRight, 1));

    private void PlaceThumb()
    {
        var vertical = ScrollsY && MaxScrollY > 0;
        var horizontal = !vertical && ScrollsX && MaxScrollX > 0;
        if ((!vertical && !horizontal) || !ShowScrollbar)
        {
            _thumb?.IsVisible = false;
            return;
        }

        if (_thumb == null)
        {
            _thumb = new BoxPlane { Color = ScrollbarColor, BorderRadius = ThumbWidth };
            if (Drawn) Context.QueueRender(_thumb, Index + ThumbDepth);
        }

        _thumb.IsVisible = true;
        _thumb.PixelScale = Context.PixelScale;
        if (vertical)
        {
            var (top, length) = ThumbRangeY();
            _thumb.SetPosition((Computed.AbsoluteX + Computed.Width - ThumbWidth, Computed.AbsoluteY + top, 0));
            _thumb.Scale = (ThumbWidth, length, 1);
        }
        else
        {
            var length = ThumbLengthX();
            var left = MaxScrollX > 0 ? _scrollX / MaxScrollX * (Computed.Width - length) : 0;
            _thumb.SetPosition((Computed.AbsoluteX + left, Computed.AbsoluteY + Computed.Height - ThumbWidth, 0));
            _thumb.Scale = (length, ThumbWidth, 1);
        }
    }

    #endregion

    #region Layout

    protected override void OnMeasureInvalidated() => _measures.Clear();

    public override (float width, float height) Measure(float parentWidth, float parentHeight)
    {
        var key = (parentWidth, parentHeight);
        if (_measures.TryGetValue(key, out var cached)) return (cached.Width, cached.Height);

        var width = Width.Auto ? float.NaN : Width.Resolve(parentWidth);
        var height = Height.Auto ? float.NaN : Height.Resolve(parentHeight);
        if (!float.IsNaN(width)) width = Clamp(width, MinWidth, MaxWidth, parentWidth);
        if (!float.IsNaN(height)) height = Clamp(height, MinHeight, MaxHeight, parentHeight);
        if (AspectRatio > 0)
        {
            if (!float.IsNaN(width) && float.IsNaN(height)) height = width / AspectRatio;
            else if (float.IsNaN(width) && !float.IsNaN(height)) width = height * AspectRatio;
        }

        // Both sides known: the content can't change the answer, so don't pay to ask it.
        if (!float.IsNaN(width) && !float.IsNaN(height))
        {
            _measures[key] = (width, height);
            return (width, height);
        }

        var capW = float.IsNaN(width) ? Clamp(parentWidth, 0, MaxWidth, parentWidth) : width;
        var (contentWidth, contentHeight) = Arrange(capW, float.IsNaN(height) ? float.PositiveInfinity : height, false);

        var measuredWidth = float.IsNaN(width)
            ? Clamp(Math.Min(contentWidth + InsetLeft + InsetRight, capW), MinWidth, MaxWidth, parentWidth)
            : width;
        if (AspectRatio > 0 && float.IsNaN(height)) height = measuredWidth / AspectRatio;
        var measuredHeight = float.IsNaN(height) ? Clamp(contentHeight + InsetTop + InsetBottom, MinHeight, MaxHeight, parentHeight) : height;
        _measures[key] = (measuredWidth, measuredHeight);
        return (measuredWidth, measuredHeight);
    }

    protected override void DoLayout()
    {
        PlaceBackground();

        var (contentWidth, contentHeight) = Arrange(Computed.Width, Computed.Height, true);
        _contentW = contentWidth;
        _contentH = contentHeight;
        // The content shrank under the offset: the children were just placed from the old one.
        var scrollY = Math.Clamp(_scrollY, 0, MaxScrollY);
        var scrollX = Math.Clamp(_scrollX, 0, MaxScrollX);
        if (Math.Abs(scrollY - _scrollY) > 0.01f || Math.Abs(scrollX - _scrollX) > 0.01f)
        {
            _scrollY = scrollY;
            _scrollX = scrollX;
            Arrange(Computed.Width, Computed.Height, true);
        }

        PlaceThumb();
        ApplyClip(_inheritedClip);
        AfterLayout?.Invoke();
    }

    /// <summary>
    ///     One flex pass. With <paramref name="commit" /> false it only answers how much room the
    ///     content takes: a row has to settle its widths before the heights of what wraps in it
    ///     mean anything, so measuring costs the same as placing.
    /// </summary>
    protected virtual (float Width, float Height) Arrange(float width, float height, bool commit)
    {
        var flow = new List<UIElement>();
        var placed = new List<UIElement>();
        var regions = new List<Region>();
        Collect(Children, flow, placed, regions);

        var innerW = Math.Max(0, width - InsetLeft - InsetRight);
        var innerH = float.IsInfinity(height) ? float.PositiveInfinity : Math.Max(0, height - InsetTop - InsetBottom);

        // A region is laid over the whole panel, so its children's positions mean the same as
        // the panel's own children's.
        if (commit)
            foreach (var region in regions)
                PlaceChild(region, 0, 0, Math.Max(0, width), float.IsInfinity(height) ? 0 : height);

        var (contentWidth, contentHeight) = Direction == LayoutDirection.Horizontal
            ? Wrap ? ArrangeRowWrap(flow, innerW, innerH, commit) : ArrangeRow(flow, innerW, innerH, commit)
            : Wrap ? ArrangeColumnWrap(flow, innerW, innerH, commit) : ArrangeColumn(flow, innerW, innerH, commit);

        if (commit)
            foreach (var child in placed)
                PlaceAbsolute(child, Math.Max(0, width),
                    float.IsInfinity(height) ? contentHeight + InsetTop + InsetBottom : height);

        return (contentWidth, contentHeight);
    }

    protected static void Collect(List<UIElement> children, List<UIElement> flow, List<UIElement> placed,
        List<Region> regions)
    {
        foreach (var child in children)
        {
            if (!child.Visible) continue;
            if (child is Region region)
            {
                regions.Add(region);
                Collect(region.Children, flow, placed, regions);
                continue;
            }

            if (child.Position == Placement.Absolute || child is DropdownMenu) placed.Add(child);
            else flow.Add(child);
        }
    }

    private Justify MainJustify => _justifySet
        ? Justify
        : (Direction == LayoutDirection.Horizontal ? HorizontalAlign : VerticalAlign) switch
        {
            Align.Center => Justify.Center,
            Align.End => Justify.End,
            _ => Justify.Start
        };

    /// <summary>The cross-axis alignment. The older align settings never stretched, and still don't.</summary>
    private Align CrossAlign
    {
        get
        {
            if (_itemsSet) return AlignItems;
            var legacy = Direction == LayoutDirection.Horizontal ? VerticalAlign : HorizontalAlign;
            return legacy == Align.Stretch ? Align.Start : legacy;
        }
    }

    private Align AlignOf(UIElement child) => child.AlignSelf switch
    {
        SelfAlign.Start => Align.Start,
        SelfAlign.Center => Align.Center,
        SelfAlign.End => Align.End,
        SelfAlign.Stretch => Align.Stretch,
        _ => CrossAlign
    };

    private static float Margin(LiteralOrComputable margin, float reference) =>
        margin.Auto ? 0 : margin.Resolve(reference);

    private static float Clamp(float value, float min, LiteralOrComputable max, float reference)
    {
        if (!max.Auto) value = Math.Min(value, max.Resolve(reference));
        return Math.Max(value, Math.Max(min, 0));
    }

    private static (float Width, float Height) Size(UIElement child, float availableW, float availableH) =>
        child.Measure(availableW, float.IsInfinity(availableH) ? 100000f : availableH);

    private static (float Lead, float Between) Spread(Justify justify, float extra, int count) => justify switch
    {
        Justify.Center => (extra / 2, 0),
        Justify.End => (extra, 0),
        Justify.Between when count > 1 => (0, extra / (count - 1)),
        Justify.Around when count > 0 => (extra / count / 2, extra / count),
        Justify.Evenly when count > 0 => (extra / (count + 1), extra / (count + 1)),
        _ => (0, 0)
    };

    private (float, float) ArrangeRow(List<UIElement> items, float innerW, float innerH, bool commit)
    {
        var itemCount = items.Count;
        if (itemCount == 0) return (0, 0);
        var scrolls = ScrollsX;
        var capW = scrolls ? 100000f : innerW;
        var crossRef = float.IsInfinity(innerH) ? 0 : innerH;
        var sizes = itemCount <= 64 ? stackalloc float[itemCount] : new float[itemCount];

        float used = ColumnGap * (itemCount - 1), growth = 0;
        for (var index = 0; index < itemCount; index++)
        {
            var child = items[index];
            float basis;
            if (!child.Basis.Auto) basis = child.Basis.Resolve(innerW);
            else if (IsShare(child.Width)) basis = 0;
            else if (Sized(child.Width)) basis = child.Width.Resolve(innerW);
            else basis = Size(child, capW, innerH).Width;
            basis = Clamp(basis, child.MinWidth, child.MaxWidth, innerW);
            sizes[index] = basis;
            used += basis + Margin(child.MarginLeft, innerW) + Margin(child.MarginRight, innerW);
            growth += child.Grow;
        }

        used += Share(items, sizes, true, (scrolls ? innerW : capW) - used, innerW);
        // What the row's content needs before anything grows: its max-content width, as CSS measures
        // a flex container. Growing fills a width the row was given; it doesn't ask for one.
        var natural = used;
        var free = scrolls ? Math.Max(0, innerW - used) : capW - used;
        if (free > 0 && growth > 0)
        {
            for (var index = 0; index < itemCount; index++)
            {
                var child = items[index];
                if (child.Grow <= 0) continue;
                var grown = sizes[index] + free * child.Grow / growth;
                var clamped = Clamp(grown, child.MinWidth, child.MaxWidth, innerW);
                used += clamped - sizes[index];
                sizes[index] = clamped;
            }

            free = capW - used;
        }
        else if (free < 0 && !scrolls)
        {
            // Only what may collapse gives way, the way min-width: auto keeps everything else whole.
            float weight = 0;
            for (var index = 0; index < itemCount; index++)
                if (items[index].CanCollapseX)
                    weight += items[index].Shrink * sizes[index];
            if (weight > 0)
                for (var index = 0; index < itemCount; index++)
                {
                    var child = items[index];
                    if (!child.CanCollapseX) continue;
                    var cut = -free * child.Shrink * sizes[index] / weight;
                    var next = Math.Max(sizes[index] - cut, Math.Max(child.MinWidth, 0));
                    used += next - sizes[index];
                    sizes[index] = next;
                }

            free = capW - used;
        }

        // Cross sizes come after the widths: what wraps needs its width to know its height.
        var heights = itemCount <= 64 ? stackalloc float[itemCount] : new float[itemCount];
        float lineH = 0;
        for (var index = 0; index < itemCount; index++)
        {
            var child = items[index];
            var height = Sized(child.Height) ? child.Height.Resolve(crossRef)
                : child.AspectRatio > 0 ? sizes[index] / child.AspectRatio
                : Size(child, sizes[index], innerH).Height;
            height = Clamp(height, child.MinHeight, child.MaxHeight, crossRef);
            heights[index] = height;
            lineH = Math.Max(lineH, height + Margin(child.MarginTop, innerW) + Margin(child.MarginBottom, innerW));
        }

        if (!commit) return (Math.Min(natural, used), lineH);

        var crossH = float.IsInfinity(innerH) ? lineH : innerH;
        var autos = 0;
        foreach (var child in items)
        {
            if (child.MarginLeft.Auto) autos++;
            if (child.MarginRight.Auto) autos++;
        }

        var extra = Math.Max(free, 0);
        var (lead, between) = autos == 0 ? Spread(MainJustify, extra, itemCount) : (0, 0);
        var share = autos > 0 ? extra / autos : 0;
        var x = InsetLeft - _scrollX + lead;
        for (var index = 0; index < itemCount; index++)
        {
            var child = items[index];
            var marginTop = Margin(child.MarginTop, innerW);
            var marginBottom = Margin(child.MarginBottom, innerW);
            if (child.MarginLeft.Auto) x += share;
            x += Margin(child.MarginLeft, innerW);

            var align = AlignOf(child);
            var height = heights[index];
            if (align == Align.Stretch && !Sized(child.Height) && child.AspectRatio <= 0)
                height = Clamp(crossH - marginTop - marginBottom, child.MinHeight, child.MaxHeight, crossRef);
            var y = InsetTop + marginTop - _scrollY + align switch
            {
                Align.Center => (crossH - height - marginTop - marginBottom) / 2,
                Align.End => crossH - height - marginTop - marginBottom,
                _ => 0
            };

            PlaceChild(child, x + child.TranslateX, y + child.TranslateY, sizes[index], height);
            x += sizes[index] + Margin(child.MarginRight, innerW) + ColumnGap + between;
            if (child.MarginRight.Auto) x += share;
        }

        return (used, lineH);
    }

    /// <summary>
    ///     A wrapping row: children are put on a line until the next doesn't fit, then each line is
    ///     laid out as a row of its own: its free space goes to what grows, then to justify, and its
    ///     height is its tallest child's, which align-items places the others against.
    /// </summary>
    private (float, float) ArrangeRowWrap(List<UIElement> items, float innerW, float innerH, bool commit)
    {
        var crossRef = float.IsInfinity(innerH) ? 0 : innerH;
        var itemCount = items.Count;
        var widths = new float[itemCount];
        for (var index = 0; index < itemCount; index++)
        {
            var child = items[index];
            var width = !child.Basis.Auto ? child.Basis.Resolve(innerW)
                : Sized(child.Width) ? child.Width.Resolve(innerW)
                : Size(child, innerW, innerH).Width;
            widths[index] = Clamp(width, child.MinWidth, child.MaxWidth, innerW);
        }

        float y = 0, maxW = 0;
        var at = 0;
        var firstLine = true;
        while (at < itemCount)
        {
            // The line: as many as fit, and always at least one.
            var end = at;
            float used = 0;
            while (end < itemCount)
            {
                var child = items[end];
                var extent = widths[end] + Margin(child.MarginLeft, innerW) + Margin(child.MarginRight, innerW);
                var gap = end == at ? 0 : ColumnGap;
                if (end > at && used + gap + extent > innerW + WrapSlack) break;
                used += gap + extent;
                end++;
            }

            float growth = 0;
            for (var index = at; index < end; index++) growth += items[index].Grow;
            var free = innerW - used;
            if (commit && free > 0 && growth > 0 && !float.IsInfinity(innerW))
            {
                for (var index = at; index < end; index++)
                {
                    var child = items[index];
                    if (child.Grow <= 0) continue;
                    var grown = Clamp(widths[index] + free * child.Grow / growth, child.MinWidth, child.MaxWidth, innerW);
                    used += grown - widths[index];
                    widths[index] = grown;
                }

                free = innerW - used;
            }

            var count = end - at;
            var heights = new float[count];
            float lineH = 0;
            for (var index = 0; index < count; index++)
            {
                var child = items[at + index];
                var height = Sized(child.Height) ? child.Height.Resolve(crossRef)
                    : child.AspectRatio > 0 ? widths[at + index] / child.AspectRatio
                    : Size(child, widths[at + index], innerH).Height;
                heights[index] = Clamp(height, child.MinHeight, child.MaxHeight, crossRef);
                lineH = Math.Max(lineH, heights[index] + Margin(child.MarginTop, innerW) + Margin(child.MarginBottom, innerW));
            }

            if (!firstLine) y += RowGap;
            if (commit)
            {
                var (lead, between) = Spread(MainJustify, Math.Max(free, 0), count);
                var x = lead;
                for (var index = 0; index < count; index++)
                {
                    var child = items[at + index];
                    var marginTop = Margin(child.MarginTop, innerW);
                    var marginBottom = Margin(child.MarginBottom, innerW);
                    x += Margin(child.MarginLeft, innerW);
                    var height = heights[index];
                    var align = AlignOf(child);
                    if (align == Align.Stretch && !Sized(child.Height) && child.AspectRatio <= 0)
                        height = Clamp(lineH - marginTop - marginBottom, child.MinHeight, child.MaxHeight, crossRef);
                    var offset = align switch
                    {
                        Align.Center => (lineH - height - marginTop - marginBottom) / 2,
                        Align.End => lineH - height - marginTop - marginBottom,
                        _ => 0
                    };
                    PlaceChild(child, InsetLeft + x - _scrollX + child.TranslateX,
                        InsetTop + y + marginTop + offset - _scrollY + child.TranslateY, widths[at + index], height);
                    x += widths[at + index] + Margin(child.MarginRight, innerW) + ColumnGap + between;
                }
            }

            maxW = Math.Max(maxW, used);
            y += lineH;
            firstLine = false;
            at = end;
        }

        return (maxW, y);
    }

    private static bool IsShare(LiteralOrComputable size) => !size.Auto && size.IsPercentage;

    /// <summary>
    ///     Gives each child with a percentage on the main axis its share of the space the others
    ///     left. Returns how much of it was handed out.
    /// </summary>
    private static float Share(List<UIElement> items, Span<float> sizes, bool row, float free, float reference)
    {
        if (free <= 0) return 0;
        float given = 0;
        for (var index = 0; index < items.Count; index++)
        {
            var child = items[index];
            if (!child.Basis.Auto) continue;
            var size = row ? child.Width : child.Height;
            if (!IsShare(size)) continue;
            var share = row
                ? Clamp(free * size.Value / 100f, child.MinWidth, child.MaxWidth, reference)
                : Clamp(free * size.Value / 100f, child.MinHeight, child.MaxHeight, reference);
            given += share - sizes[index];
            sizes[index] = share;
        }

        return given;
    }

    private (float, float) ArrangeColumnWrap(List<UIElement> items, float innerW, float innerH, bool commit)
    {
        float x = 0, y = 0, columnW = 0, maxH = 0;
        var first = true;
        foreach (var child in items)
        {
            var (measuredWidth, measuredHeight) = Size(child, innerW, innerH);
            var width = Clamp(!Sized(child.Width) ? measuredWidth : child.Width.Resolve(innerW), child.MinWidth, child.MaxWidth, innerW);
            var height = !Sized(child.Height)
                ? child.AspectRatio > 0 ? width / child.AspectRatio : measuredHeight
                : child.Height.Resolve(float.IsInfinity(innerH) ? 0 : innerH);
            var gap = first ? 0 : RowGap;
            if (!first && !float.IsInfinity(innerH) && y + gap + height > innerH + WrapSlack)
            {
                x += columnW + ColumnGap;
                y = 0;
                columnW = 0;
                gap = 0;
            }

            y += gap;
            if (commit)
                PlaceChild(child, InsetLeft + x - _scrollX + child.TranslateX,
                    InsetTop + y - _scrollY + child.TranslateY, width, height);
            y += height;
            maxH = Math.Max(maxH, y);
            columnW = Math.Max(columnW, width);
            first = false;
        }

        return (x + columnW, maxH);
    }

    private (float, float) ArrangeColumn(List<UIElement> items, float innerW, float innerH, bool commit)
    {
        var itemCount = items.Count;
        if (itemCount == 0) return (0, 0);
        var capH = ScrollsY ? float.PositiveInfinity : innerH;
        var mainRef = float.IsInfinity(innerH) ? 0 : innerH;
        var widths = itemCount <= 64 ? stackalloc float[itemCount] : new float[itemCount];
        var sizes = itemCount <= 64 ? stackalloc float[itemCount] : new float[itemCount];

        float used = RowGap * (itemCount - 1), growth = 0, maxW = 0;
        for (var index = 0; index < itemCount; index++)
        {
            var child = items[index];
            var available = innerW - Margin(child.MarginLeft, innerW) - Margin(child.MarginRight, innerW);
            var align = AlignOf(child);

            float width;
            if (Sized(child.Width)) width = child.Width.Resolve(innerW);
            else if (align == Align.Stretch) width = available;
            else width = Size(child, available, capH).Width;
            width = Clamp(width, child.MinWidth, child.MaxWidth, innerW);
            widths[index] = width;

            // flex: 1 in a column of no set height has nothing to share out, and min-height: auto
            // keeps it at its content in CSS. Only with a height to divide does the basis count.
            float basis;
            if (!child.Basis.Auto && !float.IsInfinity(capH)) basis = child.Basis.Resolve(mainRef);
            else if (IsShare(child.Height) && !float.IsInfinity(capH)) basis = 0;
            else if (Sized(child.Height)) basis = child.Height.Resolve(mainRef);
            else if (child.AspectRatio > 0) basis = width / child.AspectRatio;
            else basis = Size(child, width, capH).Height;
            basis = Clamp(basis, child.MinHeight, child.MaxHeight, mainRef);
            sizes[index] = basis;

            used += basis + Margin(child.MarginTop, innerW) + Margin(child.MarginBottom, innerW);
            growth += child.Grow;
            var natural = align == Align.Stretch && !commit && !Sized(child.Width) ? Size(child, available, capH).Width : width;
            maxW = Math.Max(maxW, natural + innerW - available);
        }

        if (!float.IsInfinity(capH)) used += Share(items, sizes, false, capH - used, mainRef);
        var free = float.IsInfinity(capH) ? 0 : capH - used;
        if (free > 0 && growth > 0)
        {
            for (var index = 0; index < itemCount; index++)
            {
                var child = items[index];
                if (child.Grow <= 0) continue;
                var grown = sizes[index] + free * child.Grow / growth;
                var clamped = Clamp(grown, child.MinHeight, child.MaxHeight, mainRef);
                used += clamped - sizes[index];
                sizes[index] = clamped;
            }
        }
        else if (free < 0)
        {
            // A column squeezes what scrolls or may collapse: the pane between two fixed rows.
            float weight = 0;
            for (var index = 0; index < itemCount; index++)
                if (items[index].CanCollapseY)
                    weight += items[index].Shrink * sizes[index];
            if (weight > 0)
                for (var index = 0; index < itemCount; index++)
                {
                    var child = items[index];
                    if (!child.CanCollapseY) continue;
                    var cut = -free * child.Shrink * sizes[index] / weight;
                    var next = Math.Max(sizes[index] - cut, Math.Max(child.MinHeight, 0));
                    used += next - sizes[index];
                    sizes[index] = next;
                }
        }

        if (!commit) return (maxW, used);

        free = float.IsInfinity(capH) ? 0 : capH - used;
        var autos = 0;
        foreach (var child in items)
        {
            if (child.MarginTop.Auto) autos++;
            if (child.MarginBottom.Auto) autos++;
        }

        var extra = Math.Max(free, 0);
        var (lead, between) = autos == 0 ? Spread(MainJustify, extra, itemCount) : (0, 0);
        var share = autos > 0 ? extra / autos : 0;
        var y = InsetTop - _scrollY + lead;
        for (var index = 0; index < itemCount; index++)
        {
            var child = items[index];
            if (child.MarginTop.Auto) y += share;
            y += Margin(child.MarginTop, innerW);
            var marginLeft = Margin(child.MarginLeft, innerW);
            var available = innerW - marginLeft - Margin(child.MarginRight, innerW);
            var x = InsetLeft + marginLeft - _scrollX;
            if (child.MarginLeft.Auto && child.MarginRight.Auto) x += (available - widths[index]) / 2;
            else if (child.MarginLeft.Auto) x += available - widths[index];
            else
                x += AlignOf(child) switch
                {
                    Align.Center => (available - widths[index]) / 2,
                    Align.End => available - widths[index],
                    _ => 0
                };

            PlaceChild(child, x + child.TranslateX, y + child.TranslateY, widths[index], sizes[index]);
            y += sizes[index] + Margin(child.MarginBottom, innerW) + RowGap + between;
            if (child.MarginBottom.Auto) y += share;
        }

        return (maxW, used);
    }

    /// <summary>
    ///     Whether a child's size was given. A plain panel or an image starts at a literal 0 rather than
    ///     auto, so a 0 counts as unset: stretch, aspect-ratio and both insets then size it, as they
    ///     would an auto box in CSS.
    /// </summary>
    private static bool Sized(LiteralOrComputable size) => !size.Auto && (size.Value != 0 || size.IsPercentage);

    protected static void PlaceAbsolute(UIElement child, float width, float height)
    {
        // A dropdown menu is a whole-window layer that places its own menu.
        if (child is DropdownMenu)
        {
            PlaceChild(child, 0, 0, width, height);
            return;
        }

        var (measuredWidth, measuredHeight) = child.Measure(width, height);
        // A plain panel or an image starts at a literal 0 rather than auto: with both insets set,
        // that 0 means "not set", so the insets size it as they would an auto box.
        var childWidth = Sized(child.Width) ? child.Width.Resolve(width)
            : !child.Left.Auto && !child.Right.Auto ? width - child.Left.Resolve(width) - child.Right.Resolve(width)
            : !Sized(child.Width) ? measuredWidth : 0;
        var childHeight = Sized(child.Height) ? child.Height.Resolve(height)
            : !child.Top.Auto && !child.Bottom.Auto ? height - child.Top.Resolve(height) - child.Bottom.Resolve(height)
            : child.AspectRatio > 0 ? childWidth / child.AspectRatio
            : measuredHeight;
        childWidth = Clamp(childWidth, child.MinWidth, child.MaxWidth, width);
        childHeight = Clamp(childHeight, child.MinHeight, child.MaxHeight, height);
        var x = !child.Left.Auto ? child.Left.Resolve(width) : !child.Right.Auto ? width - childWidth - child.Right.Resolve(width) : 0;
        var y = !child.Top.Auto ? child.Top.Resolve(height) : !child.Bottom.Auto ? height - childHeight - child.Bottom.Resolve(height) : 0;
        // Both insets and both margins auto centre a box held narrower than its insets (max-width), as in CSS.
        if (!child.Left.Auto && !child.Right.Auto && child.MarginLeft.Auto && child.MarginRight.Auto)
            x += (width - child.Left.Resolve(width) - child.Right.Resolve(width) - childWidth) / 2;
        if (!child.Top.Auto && !child.Bottom.Auto && child.MarginTop.Auto && child.MarginBottom.Auto)
            y += (height - child.Top.Resolve(height) - child.Bottom.Resolve(height) - childHeight) / 2;
        PlaceChild(child, x + child.TranslateX, y + child.TranslateY, childWidth, childHeight);
    }

    #endregion

    #region Drawing, clipping and hit testing

    public override void ApplyClip(Vector4i? clip)
    {
        _inheritedClip = clip;
        Background?.ClipRect = clip;
        _thumb?.ClipRect = clip;

        var own = clip;
        if (Clips)
        {
            var x = (int)Computed.AbsoluteX;
            var y = (int)Computed.AbsoluteY;
            own = IntersectClip(
                new Vector4i(x, y, x + (int)Math.Ceiling(Computed.Width), y + (int)Math.Ceiling(Computed.Height)), clip);
        }

        foreach (var child in Children) ClipChild(child, own);
    }

    private static void ClipChild(UIElement child, Vector4i? own)
    {
        // A child wholly outside the clip gets an empty one: the context skips those before they
        // cost a bind or a draw.
        if (own is { } rect && child is not Region)
        {
            var box = child.Computed;
            if (box.AbsoluteX > rect.Z || box.AbsoluteY > rect.W ||
                box.AbsoluteX + box.Width < rect.X || box.AbsoluteY + box.Height < rect.Y)
            {
                child.ApplyClip(new Vector4i(0, 0, 0, 0));
                return;
            }
        }

        child.ApplyClip(own);
    }

    /// <summary>
    ///     Later children are drawn over earlier ones, so they're asked first, whatever their depth:
    ///     a card's badge takes the click meant for it, not the sleeve under it.
    /// </summary>
    public override UIElement? HitTest(float x, float y)
    {
        if (!Visible || PointerEvents == PointerEvents.None) return null;
        var inside = ContainsPoint(x, y);
        if (Clips && !inside) return null;
        if (ThumbHit(x, y)) return this;

        // Raised children first, highest first; then the rest, last drawn first.
        var raised = false;
        foreach (var child in Children)
            if (child.ZIndex > 0)
            {
                raised = true;
                break;
            }

        if (raised)
            // Reversed, then a stable sort: the highest first, and the later of two equals first.
            foreach (var child in Enumerable.Reverse(Children).Where(child => child.ZIndex > 0).OrderByDescending(child => child.ZIndex))
                if (child.HitTest(x, y) is { } hit)
                    return hit;

        for (var index = Children.Count - 1; index >= 0; index--)
        {
            if (Children[index].ZIndex > 0) continue;
            var hit = Children[index].HitTest(x, y);
            if (hit != null) return hit;
        }

        return inside && PointerEvents == PointerEvents.Auto ? this : null;
    }

    protected override void DrawSelf(UIContext ctx)
    {
        base.DrawSelf(ctx);
        if (_thumb != null) ctx.QueueRender(_thumb, Index + ThumbDepth);
    }

    public override void StopRendering()
    {
        base.StopRendering();
        if (_thumb != null) Context.DequeueRender(_thumb, Index + ThumbDepth);
    }

    #endregion

    protected override void ApplyStyleValue(StyleSheet styleSheet, IStyleValue? styleValue, PropertyInfo propertyInfo)
    {
        if (styleValue is null) return;

        switch (styleValue)
        {
            case StringValue stringValue when propertyInfo.PropertyType == typeof(Align):
            {
                Align? align = stringValue.Value switch
                {
                    "center" => Align.Center,
                    "end" => Align.End,
                    "stretch" => Align.Stretch,
                    "start" => Align.Start,
                    _ => null
                };

                if (align is not null)
                    propertyInfo.SetValue(this, align.Value);
                return;
            }

            case StringValue stringValue when propertyInfo.PropertyType == typeof(LayoutDirection):
            {
                LayoutDirection? direction = stringValue.Value switch
                {
                    "horizontal" or "row" => LayoutDirection.Horizontal,
                    "vertical" or "column" => LayoutDirection.Vertical,
                    _ => null
                };

                if (direction is not null)
                    propertyInfo.SetValue(this, direction.Value);
                return;
            }

            default:
                base.ApplyStyleValue(styleSheet, styleValue, propertyInfo);
                return;
        }
    }
}
