using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace Sundex.Components.Abstractions;

/// <summary>
///     Fades a subtree by scaling every colour's alpha against the value it was styled
///     with, so a surface meant to be translucent (a 95% strip, a checkbox's box) still is
///     once the fade lands on 1. Labels and panel fills are the only things that carry
///     colour; a checkbox also owns an off-tree tick plane, which is why it gets its own
///     case.
///     <br /><br />
///     One instance per faded tree: the styled alphas are remembered on first touch, and
///     sharing an instance across trees would only grow the table for no gain.
/// </summary>
public sealed class ElementAlpha
{
    private readonly Dictionary<object, float> _base = [];

    /// <summary>
    ///     The stops each gradient was styled with. A gradient keeps its colour in its stops
    ///     and never reads <see cref="Renderable.Color" />, so it needs the whole set
    ///     remembered rather than the single alpha a solid fill gets.
    /// </summary>
    private readonly Dictionary<GradientPlane, Vector4[]> _baseStops = [];

    /// <summary>
    ///     Scales the subtree's alpha to <paramref name="alpha" />. Re-applying is cheap and
    ///     expected: <see cref="UIElement.SetClass" /> and the hovered/pressed state
    ///     overrides both re-run the stylesheet, which puts the styled alpha back, so a
    ///     tree that is mid-fade has to be re-applied after every update pass.
    /// </summary>
    public void Apply(UIElement element, float alpha)
    {
        switch (element)
        {
            case Label label:
                label.Color = label.Color with { W = Styled(label, label.Color.W) * alpha };
                return;

            case Checkbox checkbox:
                checkbox.CheckColor = checkbox.CheckColor with
                {
                    W = Styled(checkbox, checkbox.CheckColor.W) * alpha
                };
                break;
        }

        if (element is not Panel panel) return;

        switch (panel.Background)
        {
            // Scaling Color here would do nothing at all: the gradient shader is fed the
            // stops, and Color is never read. Fading one means fading every stop.
            case GradientPlane gradient:
                FadeStops(gradient, alpha);
                break;

            case { } background:
                background.Color = background.Color with { W = Styled(background, background.Color.W) * alpha };
                break;
        }

        foreach (var child in panel.Children) Apply(child, alpha);
    }

    private void FadeStops(GradientPlane gradient, float alpha)
    {
        if (!_baseStops.TryGetValue(gradient, out var styled))
            _baseStops[gradient] = styled = [.. gradient.GradientColors];

        var count = Math.Min(gradient.GradientColors.Count, styled.Length);
        for (var index = 0; index < count; index++)
            gradient.GradientColors[index] = styled[index] with { W = styled[index].W * alpha };
    }

    /// <summary>The styled alpha of a colour source, remembered the first time it is faded.</summary>
    private float Styled(object key, float current)
    {
        if (_base.TryGetValue(key, out var stored)) return stored;
        _base[key] = current;
        return current;
    }
}
