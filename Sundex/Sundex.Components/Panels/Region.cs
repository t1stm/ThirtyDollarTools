using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;

namespace Sundex.Components.Panels;

/// <summary>
///     The children of a markup <c>&lt;if&gt;</c> or <c>&lt;each&gt;</c>. Inside a
///     <see cref="FlexPanel" /> it takes no space of its own: its children are laid out as that
///     panel's (CSS <c>display: contents</c>). Anywhere else it is an ordinary flex box.
/// </summary>
[MarkupTag("region")]
public class Region(UIContext context) : FlexPanel(context)
{
    public override string Tag => "region";

    /// <summary>Whether a flex parent is laying the children out, which leaves this nothing to do.</summary>
    public bool IsContents => Parent is FlexPanel;

    protected override void DoLayout()
    {
        if (!IsContents) base.DoLayout();
    }

    // The parent lays these children out right after placing the region, so they being marked
    // once the region's own pass is over is expected, not a reason for another pass.
    protected override bool ChildrenNeedLayout() => !IsContents && base.ChildrenNeedLayout();

    /// <summary>A region has no area of its own to click: only its children do.</summary>
    public override UIElement? HitTest(float x, float y)
    {
        if (!IsContents) return base.HitTest(x, y);
        if (!Visible || PointerEvents == PointerEvents.None) return null;
        for (var index = Children.Count - 1; index >= 0; index--)
        {
            var hit = Children[index].HitTest(x, y);
            if (hit != null) return hit;
        }

        return null;
    }

    /// <summary>Disposes every child: what a branch or a row built ends with it.</summary>
    public void Clear()
    {
        for (var index = Children.Count - 1; index >= 0; index--) Children[index].Dispose();
    }

    /// <summary>Puts the children in the given order, keeping the ones already here.</summary>
    public void Order(IReadOnlyList<UIElement> order)
    {
        var same = order.Count == Children.Count;
        for (var index = 0; same && index < order.Count; index++) same = ReferenceEquals(order[index], Children[index]);
        if (same) return;
        Children.Clear();
        Children.AddRange(order);
        InvalidateMeasure();
    }
}
