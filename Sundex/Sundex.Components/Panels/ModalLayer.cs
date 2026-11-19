using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using Sundex.Components.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;

namespace Sundex.Components.Panels;

/// <summary>
///     Full-viewport top z-layer: wins every hit-test, so clicks, hover, and wheel never
///     reach the UI beneath. Content children are centered (FlexPanel). Clicking the
///     backdrop (outside all children) fires <see cref="OnDismissRequested" /> - the owner
///     decides whether to close. Open/close by adding/removing it on the scene root
///     (RemoveChild fully un-renders the subtree via the StopRendering cascade).
///     <see cref="UIElement.OnClick" /> is used internally - don't overwrite it.
/// </summary>
[Sundex.Components.Attributes.MarkupTag("modal")]
public class ModalLayer : FlexPanel
{
    /// <summary>Hit-test/render index pinned above everything a scene normally produces.</summary>
    public const int TopLayerIndex = 1000;

    public ModalLayer(UIContext context) : base(context)
    {
        Width = LiteralOrComputable.Percent(100);
        Height = LiteralOrComputable.Percent(100);
        HorizontalAlign = Align.Center;
        VerticalAlign = Align.Center;
        Background = new ColoredPlane { Color = new Vector4(0, 0, 0, 0.6f) };
        OnClick = _ =>
        {
            foreach (var child in Children)
                if (child.ContainsPoint(Context.PointerX, Context.PointerY))
                    return;

            OnDismissRequested?.Invoke(this);
        };
    }

    public override string Tag => "modal";


    /// <summary>Pinned to the top layer regardless of where the modal is parented.</summary>
    public override int Index
    {
        get => TopLayerIndex;
        internal set
        {
            foreach (var child in Children) child.Index = TopLayerIndex + 1;
        }
    }

    /// <summary>Fired when the backdrop (not the content) is clicked.</summary>
    public Action<ModalLayer>? OnDismissRequested { get; set; }

    /// <summary>
    ///     The content's primary action, for the owner's Enter key. Null when the content has
    ///     none; the layer never fires it itself.
    /// </summary>
    public Action<ModalLayer>? OnConfirmRequested { get; set; }

    /// <summary>
    ///     The content's own keys, for content that can't take them itself (a markup-built
    ///     frame, a menu's rows). Return true for a key it used. Tried before the content's own
    ///     HandleKeyDown.
    /// </summary>
    public Func<KeyboardKeyEventArgs, bool>? OnKeyRequested { get; set; }

    /// <summary>
    ///     Hands its content a key that no focused field took, so a dialog can answer the
    ///     keyboard without holding focus. The modal is never focused itself: the owner calls this.
    /// </summary>
    public override bool HandleKeyDown(KeyboardKeyEventArgs args)
    {
        if (OnKeyRequested?.Invoke(args) == true) return true;
        foreach (var child in Children)
            if (child.HandleKeyDown(args))
                return true;
        return false;
    }

    public override bool HandleScroll(Vector2 scrollDelta)
    {
        return true; // consume so nothing beneath scrolls
    }

    private bool _contentFocused;

    /// <summary>Focuses the first control when shown. Hosts with their own dialog shortcuts can opt out.</summary>
    public bool AutoFocusContent { get; set; } = true;

    /// <summary>
    ///     The first time it is laid out, the modal hands the focus to its first focusable content,
    ///     as a browser's showModal() does, unless the focus is already inside it (bug 2.11).
    /// </summary>
    protected override void DoLayout()
    {
        base.DoLayout();
        if (_contentFocused || !AutoFocusContent) return;
        _contentFocused = true;

        for (var at = Context.FocusedElement; at != null; at = at.Parent)
            if (ReferenceEquals(at, this)) return;
        if (FirstFocusable(this) is { } first) Context.Focus(first);
    }

    private static UIElement? FirstFocusable(UIElement element)
    {
        if (!element.Visible || element.Disabled) return null;
        if (element.Focusable && element is not ModalLayer) return element;
        if (element is not Panel panel) return null;
        foreach (var child in panel.Children)
            if (FirstFocusable(child) is { } found)
                return found;
        return null;
    }
}
