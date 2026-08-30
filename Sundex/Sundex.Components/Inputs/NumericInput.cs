using System.Globalization;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;

namespace Sundex.Components.Inputs;

/// <summary>
///     Numeric spinner: a <see cref="TextInput" /> in numeric mode with +/− step buttons,
///     min/max clamping, Up/Down key stepping, and nullable-value support
///     (empty text commits as null when <see cref="AllowNull" /> is set).
///     While typing, <see cref="Value" /> parses the raw text live; the text itself is
///     normalized (clamped/reformatted, or reverted to the last committed value) on blur.
/// </summary>
public class NumericInput : TextInput
{
    private readonly SpinnerButton _minus;
    private readonly SpinnerButton _plus;
    private double _lastValid;

    public NumericInput(UIContext context, double? value = null) : base(context)
    {
        Filter = TextInputFilter.Decimal;
        _minus = new SpinnerButton(context, "-") { OnClick = _ => StepBy(-1) };
        _plus = new SpinnerButton(context, "+") { OnClick = _ => StepBy(1) };
        Children = [.. Children, _minus, _plus];
        Value = value;
    }

    public override string Tag => "numeric-input";

    public double Min { get; set; } = double.MinValue;
    public double Max { get; set; } = double.MaxValue;

    /// <summary>Amount added/subtracted by the buttons and the Up/Down keys.</summary>
    public double Step { get; set; } = 1;

    /// <summary>When set, empty text commits as null instead of reverting to the last value.</summary>
    public bool AllowNull { get; set; }

    /// <summary>The raw text (this class hides the string <see cref="TextInput.Value" />).</summary>
    public string Text => base.Value;

    /// <summary>Parsed and clamped value of the current text; null when unparseable/empty.</summary>
    public new double? Value
    {
        get => double.TryParse(base.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? Math.Clamp(v, Min, Max)
            : null;
        set
        {
            if (value is { } v)
            {
                _lastValid = Math.Clamp(v, Min, Max);
                base.Value = _lastValid.ToString("0.#########", CultureInfo.InvariantCulture);
            }
            else
            {
                base.Value = "";
            }
        }
    }

    private float ButtonSize => Math.Max(0, Computed.Height - 2 * Padding);

    /// <summary>Keeps the text viewport clear of the two buttons at the right end.</summary>
    protected override float ReservedRightWidth => 2 * ButtonSize;

    public void StepBy(int direction)
    {
        // ponytail: rounding kills accumulated binary noise from repeated fractional steps.
        Value = Math.Round((Value ?? _lastValid) + direction * Step, 9);
    }

    public override bool HandleKeyDown(KeyboardKeyEventArgs e)
    {
        switch (e.Key)
        {
            case Keys.Up:
                StepBy(1);
                return true;

            case Keys.Down:
                StepBy(-1);
                return true;

            default:
                return base.HandleKeyDown(e);
        }
    }

    protected override void FocusLost()
    {
        base.FocusLost();
        // Normalize: reformat a valid value, revert garbage/empty to the last committed
        // value, or keep it empty (null) when AllowNull. Assigned unconditionally, because
        // text that parsed fine still needs the reformat - "150" typed against a Max of
        // 100 has to show 100, not its raw text.
        Value = Value ?? (AllowNull ? null : _lastValid);
    }

    protected override void DoLayout()
    {
        var innerWidth = Math.Max(0, Computed.Width - 2 * Padding);
        var size = ButtonSize;

        _minus.Width = _plus.Width = size;
        _minus.Height = _plus.Height = size;
        _minus.Y = _plus.Y = 0;
        _minus.X = innerWidth - 2 * size;
        _plus.X = innerWidth - size;

        base.DoLayout();
    }

    /// <summary>Consumes presses so the text field doesn't move its caret under the buttons.</summary>
    private sealed class SpinnerButton(UIContext context, string label)
        : Button(context, label, new ColoredPlane { Color = new Vector4(1, 1, 1, 0.15f) })
    {
        public override bool HandlePress(float x, float y)
        {
            return true;
        }

        // Rapid-clicking a step button is a double-press; don't let it bubble into the
        // text field and flash a selection of the number.
        public override bool HandleDoublePress(float x, float y)
        {
            return true;
        }
    }
}