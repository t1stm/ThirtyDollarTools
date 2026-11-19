using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;

namespace Sundex.Components.Abstractions;

/// <summary>How a flex container spreads free space along its main axis (CSS <c>justify-content</c>).</summary>
public enum Justify
{
    Start,
    Center,
    End,
    Between,
    Around,
    Evenly
}

/// <summary>Where a child sits on its container's cross axis, overriding the container's <c>align-items</c>.</summary>
public enum SelfAlign
{
    Auto,
    Start,
    Center,
    End,
    Stretch
}

/// <summary>CSS <c>position</c>: in the flow, or placed by its insets against the parent.</summary>
public enum Placement
{
    Flow,
    Absolute
}

/// <summary>What happens to children that don't fit (CSS <c>overflow</c>).</summary>
public enum Overflow
{
    Visible,
    Hidden,
    Scroll
}

/// <summary>Which part of an element takes the pointer (CSS <c>pointer-events</c>, plus a children-only mode).</summary>
public enum PointerEvents
{
    Auto,

    /// <summary>Neither the element nor anything in it.</summary>
    None,

    /// <summary>Not the element's own area, but its children: an overlay that only catches what's on it.</summary>
    Children
}

/// <summary>
///     What a flex container reads from each child. These live on every element rather than on the
///     container, so a label, an image or an input is as much a flex item as a panel is.
/// </summary>
public abstract partial class UIElement
{
    private bool _laying;

    /// <summary>The narrowest flex shrinking may take this; -1 keeps it at its content (CSS <c>min-width: auto</c>).</summary>
    [NamedSetting("min-width")]
    public float MinWidth
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = -1;

    /// <inheritdoc cref="MinWidth" />
    [NamedSetting("min-height")]
    public float MinHeight
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = -1;

    /// <summary>The widest this may get; auto for no limit.</summary>
    [NamedSetting("max-width")]
    public LiteralOrComputable MaxWidth
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = LiteralOrComputable.AutoSize;

    /// <inheritdoc cref="MaxWidth" />
    [NamedSetting("max-height")]
    public LiteralOrComputable MaxHeight
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = LiteralOrComputable.AutoSize;

    [NamedSetting("grow")]
    public float Grow
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    [NamedSetting("shrink")]
    public float Shrink
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = 1;

    /// <summary>The main-axis size flex starts from before growing or shrinking; auto for the content's.</summary>
    [NamedSetting("basis")]
    public LiteralOrComputable Basis
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = LiteralOrComputable.AutoSize;

    [NamedSetting("align-self")]
    public SelfAlign AlignSelf
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = SelfAlign.Auto;

    /// <summary>A margin; <c>auto</c> takes the free space, which is how a child is pushed to the far end.</summary>
    [NamedSetting("margin-top")]
    public LiteralOrComputable MarginTop
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    /// <inheritdoc cref="MarginTop" />
    [NamedSetting("margin-right")]
    public LiteralOrComputable MarginRight
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    /// <inheritdoc cref="MarginTop" />
    [NamedSetting("margin-bottom")]
    public LiteralOrComputable MarginBottom
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    /// <inheritdoc cref="MarginTop" />
    [NamedSetting("margin-left")]
    public LiteralOrComputable MarginLeft
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    /// <summary>All four margins at once.</summary>
    [NamedSetting("margin")]
    public LiteralOrComputable Margin
    {
        get => MarginTop;
        set => MarginTop = MarginRight = MarginBottom = MarginLeft = value;
    }

    [NamedSetting("position")]
    public Placement Position
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = Placement.Flow;

    /// <summary>An inset for <c>position: absolute</c>; auto when unset. Left and right together set the width.</summary>
    [NamedSetting("left")]
    public LiteralOrComputable Left
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = LiteralOrComputable.AutoSize;

    /// <inheritdoc cref="Left" />
    [NamedSetting("top")]
    public LiteralOrComputable Top
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = LiteralOrComputable.AutoSize;

    /// <inheritdoc cref="Left" />
    [NamedSetting("right")]
    public LiteralOrComputable Right
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = LiteralOrComputable.AutoSize;

    /// <inheritdoc cref="Left" />
    [NamedSetting("bottom")]
    public LiteralOrComputable Bottom
    {
        get;
        set => UpdateSetDirty(ref field, value);
    } = LiteralOrComputable.AutoSize;

    /// <summary>Width over height when only one of them is known; 0 for none. 1 makes a square.</summary>
    [NamedSetting("aspect-ratio")]
    public float AspectRatio
    {
        get;
        set => UpdateSetDirty(ref field, value);
    }

    /// <summary>An offset from where layout put this, that moves nothing else (CSS <c>translate</c>).</summary>
    [NamedSetting("translate-x")]
    public float TranslateX
    {
        get;
        set => UpdateSetPosition(ref field, value);
    }

    /// <inheritdoc cref="TranslateX" />
    [NamedSetting("translate-y")]
    public float TranslateY
    {
        get;
        set => UpdateSetPosition(ref field, value);
    }

    [NamedSetting("pointer-events")]
    public PointerEvents PointerEvents { get; set; } = PointerEvents.Auto;

    /// <summary>
    ///     How far above its parent's depth this element and its subtree draw: a menu or a sheet over
    ///     the page. Depth decides draw order across the whole tree, and a flex panel asks children
    ///     with a higher z-index first when hit testing.
    /// </summary>
    [NamedSetting("z-index")]
    public int ZIndex
    {
        get;
        set
        {
            if (field == value) return;
            var drawn = Drawn;
            if (drawn) StopRendering();
            field = value;
            Index = (Parent?.Index + 1 ?? 0) + value;
            if (drawn) DrawTo(Context);
        }
    }

    /// <summary>Whether flex shrinking may take this below its content on the horizontal axis.</summary>
    public virtual bool CanCollapseX => MinWidth == 0;

    /// <summary>Whether flex shrinking may take this below its content on the vertical axis.</summary>
    public virtual bool CanCollapseY => MinHeight == 0;

    /// <summary>
    ///     Says that what this element contains changed size: drops every measurement cached for it
    ///     and its ancestors, then marks it for layout. A position or a size its parent writes needs
    ///     only <see cref="InvalidateLayout" />; content, children, text and size settings need this.
    /// </summary>
    public void InvalidateMeasure()
    {
        for (var element = this; element != null; element = element.Parent) element.OnMeasureInvalidated();
        InvalidateLayout();
    }

    /// <summary>Drops whatever measurements the element caches. Called by <see cref="InvalidateMeasure" />.</summary>
    protected virtual void OnMeasureInvalidated()
    {
    }

    protected void UpdateSetPosition<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        InvalidateLayout();
    }

    /// <summary>Places and sizes a child from inside this element's layout pass, and lays it out.</summary>
    protected internal static void PlaceChild(UIElement child, float x, float y, float width, float height)
    {
        child.X = x;
        child.Y = y;
        child.ParentAssignedWidth = width;
        child.ParentAssignedHeight = height;
        child.Layout();
    }}
