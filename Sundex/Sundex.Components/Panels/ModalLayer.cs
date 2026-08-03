using OpenTK.Mathematics;
using Shared.Renderer.Planes;
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
public class ModalLayer : FlexPanel
{
    /// <summary>Hit-test/render index pinned above everything a scene normally produces.</summary>
    public const int TopLayerIndex = 1000;

    public ModalLayer(UIContext context) : base(context)
    {
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

    public override LiteralOrComputable Width { get; set; } = LiteralOrComputable.Percent(100);
    public override LiteralOrComputable Height { get; set; } = LiteralOrComputable.Percent(100);

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

    public override bool HandleScroll(Vector2 scrollDelta)
    {
        return true; // consume so nothing beneath scrolls
    }
}