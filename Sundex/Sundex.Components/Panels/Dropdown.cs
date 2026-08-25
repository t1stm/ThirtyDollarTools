using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;

namespace Sundex.Components.Panels;

/// <summary>The visible menu box. Styled by <c>component dropdown { }</c>.</summary>
public class Dropdown : FlexPanel
{
    public Dropdown(UIContext context) : base(context)
    {
        Direction = LayoutDirection.Vertical;
        Padding = 4;
    }

    public override string Tag => "dropdown";

    protected override void DoLayout()
    {
        base.DoLayout();

        // Cross-axis stretch: FlexPanel's Align.Stretch only zeroes X (its resize line is
        // commented out as destructive), so narrow items would leave ragged hover
        // highlights. ParentAssignedWidth is pass-scoped - FlexPanel.DoLayout clears it at
        // the top of every pass - and Measure never reads it, so an auto-width box still
        // sizes to its widest item with no feedback loop.
        var inner = Math.Max(0, Computed.Width - 2 * Padding);
        foreach (var child in Children)
        {
            child.ParentAssignedWidth = inner;
            child.Layout();
        }
    }
}

/// <summary>One menu row. Styled by <c>component dropdown-item { }</c>.</summary>
public class DropdownItem : Button
{
    public DropdownItem(UIContext context, string text) : base(context, text)
    {
        // Button centers its label; a menu row reads down a left edge instead, so rows of
        // different lengths line up under each other.
        HorizontalAlign = Align.Start;
    }

    public override string Tag => "dropdown-item";
}

/// <summary>
///     A popup menu anchored either at a cursor position (a right-click context menu) or
///     under an element (a select's drop-down, see <see cref="Below" />). Add it to the
///     scene root to open it; it dismisses itself like any other <see cref="ModalLayer" />
///     (outside click, Escape via DialogHost) and blocks input to everything beneath while
///     open. The box flips to the other side of its anchor rather than leave the viewport.
/// </summary>
public class DropdownMenu : ModalLayer
{
    /// <summary>Gap between the cursor and the menu's near edge.</summary>
    private const float CursorGap = 2;

    private readonly UIElement? _anchorElement;
    private readonly float _anchorX, _anchorY;
    private readonly int _openedOnRightPressId;

    /// <summary>Anchors the menu at a point - a right-click's cursor position.</summary>
    public DropdownMenu(UIContext context, float x, float y) : this(context)
    {
        _anchorX = x;
        _anchorY = y;
    }

    private DropdownMenu(UIContext context, UIElement anchor) : this(context)
    {
        _anchorElement = anchor;
    }

    private DropdownMenu(UIContext context) : base(context)
    {
        _openedOnRightPressId = context.RightPressId;

        Background = null; // invisible backdrop, still wins HitTest and blocks input
        HorizontalAlign = Align.Start;
        VerticalAlign = Align.Start;

        AddChild(Menu = new Dropdown(context));
        OnDismissRequested = m => (m.Parent as Panel)?.RemoveChild(m);
    }

    public override string Tag => "dropdown-layer";

    /// <summary>
    ///     Anchors the menu to an element's own edges - flush under its bottom-left, or
    ///     right-aligned/above it near a viewport edge. What a select control wants: the
    ///     pointer's exact position inside the button is not where its list should hang.
    /// </summary>
    public static DropdownMenu Below(UIElement element)
    {
        return new DropdownMenu(element.Context, element);
    }

    /// <summary>The styled menu box. Items go here.</summary>
    public Dropdown Menu { get; }

    /// <summary>Adds a row; clicking it closes the menu, then runs <paramref name="action" />.</summary>
    public DropdownItem AddItem(string text, Action action)
    {
        var item = new DropdownItem(Context, text) { OnClick = _ => { Close(); action(); } };
        Menu.AddChild(item);
        return item;
    }

    public void Close()
    {
        OnDismissRequested?.Invoke(this);
    }

    /// <summary>
    ///     A right-click outside the menu closes it. Right-press is level-triggered, so the
    ///     press that opened this menu (still held on the first frames) must not immediately
    ///     close it - hence the RightPressId guard.
    /// </summary>
    public override bool HandleRightPress(float x, float y)
    {
        if (Context.RightPressId != _openedOnRightPressId && !Menu.ContainsPoint(x, y)) Close();
        return true;
    }

    protected override void DoLayout()
    {
        base.DoLayout(); // sizes Menu from its content, and parks it at 0,0

        var w = Menu.Computed.Width;
        var h = Menu.Computed.Height;

        // "Near" is the preferred edge to hang from, "far" the one a flip hangs from. For a
        // cursor both are the same point, offset by the gap either side; for an element the
        // near/far pair is its own box, so a flip right-aligns to it (or sits above it)
        // instead of jumping the button's width away.
        float nearX, farX, nearY, farY;
        if (_anchorElement is { } anchor)
        {
            var box = anchor.Computed;
            (nearX, farX) = (box.AbsoluteX, box.AbsoluteX + box.Width);
            (nearY, farY) = (box.AbsoluteY + box.Height, box.AbsoluteY);
        }
        else
        {
            (nearX, farX) = (_anchorX + CursorGap, _anchorX - CursorGap);
            (nearY, farY) = (_anchorY, _anchorY);
        }

        // Children are positioned relative to this layer's own origin.
        nearX -= Computed.AbsoluteX;
        farX -= Computed.AbsoluteX;
        nearY -= Computed.AbsoluteY;
        farY -= Computed.AbsoluteY;

        var x = nearX;
        if (x + w > Computed.Width) x = farX - w; // flip to the left

        var y = nearY;
        if (y + h > Computed.Height) y = farY - h; // flip upwards

        // Flip first, clamp second: a menu larger than the viewport overflows both ways.
        Menu.X = Math.Clamp(x, 0, Math.Max(0, Computed.Width - w));
        Menu.Y = Math.Clamp(y, 0, Math.Max(0, Computed.Height - h));
        // FlexPanel.DoLayout overwrote X/Y above, so placement must re-Layout the child.
        // Loop-safe: InvalidateLayout short-circuits while our own NeedsLayout is still
        // true (it is cleared after DoLayout returns).
        Menu.Layout();
    }
}
