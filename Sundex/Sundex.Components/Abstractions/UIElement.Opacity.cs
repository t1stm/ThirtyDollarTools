using Sundex.Components.Attributes;

namespace Sundex.Components.Abstractions;

/// <summary>
///     Opacity that a subtree inherits, as in CSS: an element shows at its own opacity times every
///     ancestor's. Each element applies <see cref="EffectiveOpacity" /> to the colours it draws, so
///     a fade is one setting on the root of what fades, not a walk over everything in it.
/// </summary>
public abstract partial class UIElement
{
    [NamedSetting("opacity")]
    public float Opacity
    {
        get;
        set
        {
            if (Math.Abs(field - value) < 0.0001f) return;
            field = value;
            UpdateEffectiveOpacity();
        }
    } = 1;

    /// <summary>This element's opacity times its ancestors'.</summary>
    public float EffectiveOpacity { get; private set; } = 1;

    internal void UpdateEffectiveOpacity()
    {
        var effective = (Parent?.EffectiveOpacity ?? 1) * Opacity;
        if (Math.Abs(effective - EffectiveOpacity) < 0.0001f) return;
        EffectiveOpacity = effective;
        OnOpacityChanged();
    }

    /// <summary>Re-applies <see cref="EffectiveOpacity" /> to what the element draws; containers pass it on.</summary>
    protected virtual void OnOpacityChanged()
    {
    }
}
