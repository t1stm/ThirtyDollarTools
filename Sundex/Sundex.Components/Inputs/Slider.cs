using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Bars;

namespace Sundex.Components.Inputs;

/// <summary>
///     Horizontal slider: a <see cref="ProgressBar" /> whose fill tracks a value in
///     [<see cref="Min" />, <see cref="Max" />], set by pressing/dragging anywhere on the bar
///     (capture keeps the drag alive outside the bounds). Optional <see cref="Step" /> snapping.
/// </summary>
public class Slider : ProgressBar
{
    public Slider(UIContext context, Renderable? background = null, Renderable? foreground = null)
        : base(context,
            background ?? new ColoredPlane { Color = new Vector4(1, 1, 1, 0.15f) },
            foreground ?? new ColoredPlane { Color = new Vector4(1, 1, 1, 0.6f) })
    {
        UpdateCursorOnHover = true;
    }

    public override string Tag => "slider";

    public double Min { get; set; }
    public double Max { get; set; } = 1;

    /// <summary>Snap increment; null (default) means continuous.</summary>
    public double? Step { get; set; }

    public Action<Slider>? OnValueChanged { get; set; }

    public double Value
    {
        get;
        set
        {
            var v = Math.Clamp(Snap(value), Min, Max);
            if (field.Equals(v)) return;
            field = v;
            Progress = Max > Min ? (float)((v - Min) / (Max - Min)) : 0;
            OnValueChanged?.Invoke(this);
        }
    }

    public override bool HandlePress(float x, float y)
    {
        SetFromPointer(x);
        return true;
    }

    public override void HandlePointerDrag(float x, float y)
    {
        SetFromPointer(x);
    }

    private void SetFromPointer(float x)
    {
        var width = Computed.Width;
        if (width <= 0) return;

        var t = Math.Clamp((x - Computed.AbsoluteX) / width, 0, 1);
        Value = Min + t * (Max - Min);
    }

    private double Snap(double value)
    {
        if (Step is not { } step || step <= 0) return value;
        // ponytail: rounding kills binary float noise from the division/multiplication.
        return Math.Round(Math.Round(value / step) * step, 9);
    }
}