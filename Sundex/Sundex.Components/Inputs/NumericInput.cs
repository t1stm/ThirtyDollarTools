using System.Globalization;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;
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
    private readonly Label _unit;
    private double _lastValid;

    public NumericInput(UIContext context, double? value = null) : base(context)
    {
        Filter = TextInputFilter.Decimal;
        _minus = new SpinnerButton(context, "-") { OnClick = _ => StepBy(-1) };
        _plus = new SpinnerButton(context, "+") { OnClick = _ => StepBy(1) };
        _unit = new Label(context, "") { FontSizePx = 12f };
        Children = [.. Children, _unit, _minus, _plus];
        Value = value;
    }

    public override string Tag => "numeric-input";

    public double Min { get; set; } = double.MinValue;
    public double Max { get; set; } = double.MaxValue;

    /// <summary>Amount added/subtracted by the buttons and the Up/Down keys.</summary>
    public double Step { get; set; } = 1;

    /// <summary>When set, empty text commits as null instead of reverting to the last value.</summary>
    public bool AllowNull { get; set; }

    /// <summary>
    ///     What a step starts from while the field is empty (see <see cref="AllowNull" />) -
    ///     the value an empty field stands for. Null starts from the last value.
    /// </summary>
    public double? EmptyValue { get; set; }

    /// <summary>
    ///     Amount one wheel notch over the field adds. Null, the default, leaves the wheel to
    ///     whatever is under the field (a scrolling list, say).
    /// </summary>
    public double? ScrollStep { get; set; }

    /// <summary>Scales <see cref="ScrollStep" /> by 0.1 while set; the owner sets it from held modifiers.</summary>
    public bool FineScroll { get; set; }

    /// <summary>The − button, for an owner that styles it (a class on it overrides its default fill).</summary>
    public Button DecrementButton => _minus;

    /// <summary>The + button; see <see cref="DecrementButton" />.</summary>
    public Button IncrementButton => _plus;

    /// <summary>
    ///     Gap between the two step buttons, and between the <see cref="Unit" /> and the first
    ///     of them; 0 puts them edge to edge.
    /// </summary>
    [NamedSetting("step-spacing")]
    public float StepSpacing { get; set; }

    /// <summary>A unit ("%", "s") shown at the right end of the text, before the step buttons. Empty shows none.</summary>
    public string Unit
    {
        get;
        set
        {
            field = value;
            _unit.SetTextContents(value);
        }
    } = "";

    [NamedSetting("unit-font-size")]
    public float UnitFontSize
    {
        get => _unit.FontSizePx.Value;
        set => _unit.FontSizePx = value;
    }

    [NamedSetting("unit-color")]
    public Vector4 UnitColor
    {
        get => _unit.Color;
        set => _unit.Color = value;
    }

    private float UnitWidth => Unit.Length == 0 ? 0 : _unit.Width.Value + StepSpacing;

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
    protected override float ReservedRightWidth => 2 * ButtonSize + StepSpacing + UnitWidth;

    public void StepBy(int direction)
    {
        StepBy(direction, Step);
    }

    private void StepBy(int direction, double step)
    {
        // ponytail: rounding kills accumulated binary noise from repeated fractional steps.
        Value = Math.Round((Value ?? EmptyValue ?? _lastValid) + direction * step, 9);
    }

    public override bool HandleScroll(Vector2 scrollDelta)
    {
        if (ScrollStep is not { } step || scrollDelta.Y == 0) return false;
        StepBy(Math.Sign(scrollDelta.Y), FineScroll ? step * 0.1 : step);
        return true;
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
        _minus.X = innerWidth - 2 * size - StepSpacing;
        _plus.X = innerWidth - size;
        _unit.X = innerWidth - 2 * size - StepSpacing - UnitWidth;
        _unit.Y = (Computed.Height - 2 * Padding - _unit.Height.Value) / 2;

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