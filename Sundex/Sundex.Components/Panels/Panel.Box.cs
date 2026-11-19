using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;
using Sundex.Core.Renderer.Planes;
using Sundex.Reactive;

namespace Sundex.Components.Panels;

/// <summary>
///     The box a panel draws: fill, a border per side, radius (<see cref="Panel.BorderRadius" />), a
///     radial glow and a frosted backdrop, on one <see cref="BoxPlane" />, faded by the panel's
///     opacity. The plane is made the first time any of these is set, so a panel that only
///     arranges its children costs no draw call.
/// </summary>
public partial class Panel
{
    private BoxPlane? _box;
    private Vector4 _fill, _fillShown, _borderColor, _glowColor;
    private Vector4 _glow = new(0.5f, 0.5f, 1, 1);
    private float _borderTop, _borderRight, _borderBottom, _borderLeft, _backdrop = -1;
    private bool _fillTicking;

    /// <summary>
    ///     The box's colour. Unlike <see cref="Panel.Background" />, which swaps in a plane of its
    ///     own, this takes the border, glow, opacity and pressed brightness with it.
    /// </summary>
    [NamedSetting("fill")]
    public Vector4 Fill
    {
        get => _fill;
        set
        {
            if (_fill == value) return;
            _fill = value;
            if (TransitionMs > 0 && _box != null && Drawn) StartFillTransition();
            else _fillShown = value;
            RefreshBox();
        }
    }

    /// <summary>How long a change of <see cref="Fill" /> takes, in ms: a CSS colour transition.</summary>
    [NamedSetting("transition")]
    public float TransitionMs { get; set; }

    [NamedSetting("border-color")]
    public Vector4 BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            RefreshBox();
        }
    }

    /// <summary>All four border widths at once.</summary>
    [NamedSetting("border-width")]
    public float BorderWidth
    {
        get => _borderTop;
        set
        {
            var resized = _borderTop != value || _borderRight != value || _borderBottom != value || _borderLeft != value;
            _borderTop = _borderRight = _borderBottom = _borderLeft = value;
            RefreshBox();
            // A flex panel lays its content out inside the border.
            if (resized) InvalidateMeasure();
        }
    }

    [NamedSetting("border-top")]
    public float BorderTop
    {
        get => _borderTop;
        set
        {
            var resized = _borderTop != value;
            _borderTop = value;
            RefreshBox();
            if (resized) InvalidateMeasure();
        }
    }

    [NamedSetting("border-right")]
    public float BorderRight
    {
        get => _borderRight;
        set
        {
            var resized = _borderRight != value;
            _borderRight = value;
            RefreshBox();
            if (resized) InvalidateMeasure();
        }
    }

    [NamedSetting("border-bottom")]
    public float BorderBottom
    {
        get => _borderBottom;
        set
        {
            var resized = _borderBottom != value;
            _borderBottom = value;
            RefreshBox();
            if (resized) InvalidateMeasure();
        }
    }

    [NamedSetting("border-left")]
    public float BorderLeft
    {
        get => _borderLeft;
        set
        {
            var resized = _borderLeft != value;
            _borderLeft = value;
            RefreshBox();
            if (resized) InvalidateMeasure();
        }
    }

    /// <summary>A radial glow inside the box, in this colour.</summary>
    [NamedSetting("glow-color")]
    public Vector4 GlowColor
    {
        get => _glowColor;
        set
        {
            _glowColor = value;
            RefreshBox();
        }
    }

    /// <summary>Where the glow sits and how far it reaches: centre x, centre y (0–1), radius x, radius y.</summary>
    [NamedSetting("glow")]
    public Vector4 Glow
    {
        get => _glow;
        set
        {
            _glow = value;
            RefreshBox();
        }
    }

    /// <summary>Blurs what's behind the box (CSS <c>backdrop-filter: blur</c>), as a mip level; -1 for none.</summary>
    [NamedSetting("backdrop-blur")]
    public float BackdropBlur
    {
        get => _backdrop;
        set
        {
            _backdrop = value;
            RefreshBox();
        }
    }

    /// <summary>How much brighter the fill gets while pressed: 1 for no change.</summary>
    [NamedSetting("press-brightness")]
    public float PressBrightness
    {
        get;
        set
        {
            field = value;
            RefreshBox();
        }
    } = 1;

    private bool WantsBox => _fill.W > 0 || _fillShown.W > 0 ||
                             (_borderColor.W > 0 && (_borderTop > 0 || _borderRight > 0 || _borderBottom > 0 || _borderLeft > 0)) ||
                             _glowColor.W > 0 || _backdrop >= 0;

    protected override void OnOpacityChanged()
    {
        base.OnOpacityChanged();
        RefreshBox();
        foreach (var child in Children) child.UpdateEffectiveOpacity();
    }

    public override void InvalidateStyle()
    {
        base.InvalidateStyle();
        if (PressBrightness != 1) RefreshBox();
    }

    private void RefreshBox()
    {
        if (_box == null)
        {
            if (!WantsBox) return;
            Background = _box = new BoxPlane();
            InvalidateLayout();
        }

        var alpha = EffectiveOpacity;
        var brightness = CurrentState == UIState.Pressed ? PressBrightness : 1;
        var fill = new Vector4(_fillShown.Xyz * brightness, _fillShown.W * alpha);
        _box.Color = fill;
        _box.BorderColor = _borderColor with { W = _borderColor.W * alpha };
        _box.BorderWidths = new Vector4(_borderTop, _borderRight, _borderBottom, _borderLeft);
        _box.GlowColor = _glowColor with { W = _glowColor.W * alpha };
        _box.Glow = _glow;
        _box.BackdropLod = _backdrop;
        _box.PixelScale = Context.PixelScale;
        _box.IsVisible = WantsBox && alpha > 0;
    }

    private void StartFillTransition()
    {
        var from = _fillShown;
        var to = _fill;
        var start = Frame.Now;
        if (_fillTicking) return;
        _fillTicking = true;
        Frame.Tick(() =>
        {
            if (to != _fill)
            {
                from = _fillShown;
                to = _fill;
                start = Frame.Now;
            }

            var progress = (float)Math.Clamp((Frame.Now - start) / Math.Max(TransitionMs, 1), 0, 1);
            _fillShown = Vector4.Lerp(from, to, progress);
            RefreshBox();
            if (progress < 1) return true;
            _fillTicking = false;
            return false;
        });
    }
}
