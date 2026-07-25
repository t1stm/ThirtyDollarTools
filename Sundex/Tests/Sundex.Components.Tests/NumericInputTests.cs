using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Inputs;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class NumericInputTests
{
    private static (TestUIContext ctx, Panel root, NumericInput input) NewInput(double? value = null,
        float width = 200)
    {
        var ctx = new TestUIContext();
        var root = new Panel(ctx) { Width = 800, Height = 600 };
        var input = new NumericInput(ctx, value) { X = 10, Y = 10, Width = width };
        root.Children = [input];
        root.Layout();
        return (ctx, root, input);
    }

    private static void Type(TestUIContext ctx, string text)
    {
        foreach (var c in text) ctx.DispatchTextInput(new TextInputEventArgs(c));
    }

    private static void Key(TestUIContext ctx, Keys key, KeyModifiers modifiers = 0)
    {
        ctx.DispatchKeyDown(new KeyboardKeyEventArgs(key, 0, modifiers, false));
    }

    private static void Click(TestUIContext ctx, Panel root, float x, float y)
    {
        ctx.UpdatePointer(root, x, y, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, x, y, false, false, true, Vector2.Zero);
    }

    // Input at (10,10), 200x32, Padding 6 -> content spans x 16..204, buttons are two
    // 20x20 squares flush right: minus 164..184, plus 184..204, y 16..36.
    private const float MinusX = 174;
    private const float PlusX = 194;
    private const float ButtonY = 20;

    [Fact]
    public void Value_ParsesTypedTextAndClamps()
    {
        var (ctx, _, input) = NewInput();
        input.Max = 100;

        ctx.Focus(input);
        Type(ctx, "150");

        Assert.Equal("150", input.Text); // raw text untouched while typing
        Assert.Equal(100, input.Value); // parsed value clamps live
    }

    [Fact]
    public void Blur_NormalizesTextToClampedValue()
    {
        var (ctx, _, input) = NewInput();
        input.Max = 100;

        ctx.Focus(input);
        Type(ctx, "150");
        Key(ctx, Keys.Enter); // commits and blurs

        Assert.Equal("100", input.Text);
    }

    [Fact]
    public void Blur_RevertsGarbageToLastValue_OrNullWhenAllowed()
    {
        var (ctx, _, input) = NewInput(42);
        ctx.Focus(input);
        Key(ctx, Keys.A, KeyModifiers.Control);
        Key(ctx, Keys.Backspace);
        Key(ctx, Keys.Escape); // blurs

        Assert.Equal(42, input.Value);
        Assert.Equal("42", input.Text);

        input.AllowNull = true;
        ctx.Focus(input);
        Key(ctx, Keys.A, KeyModifiers.Control);
        Key(ctx, Keys.Backspace);
        Key(ctx, Keys.Escape);

        Assert.Null(input.Value);
        Assert.Equal("", input.Text);
    }

    [Fact]
    public void UpDownKeys_StepAndClamp()
    {
        var (ctx, _, input) = NewInput(5);
        input.Min = 3;
        input.Max = 7;

        ctx.Focus(input);
        Key(ctx, Keys.Up);
        Assert.Equal(6, input.Value);

        Key(ctx, Keys.Up);
        Key(ctx, Keys.Up); // clamps at Max
        Assert.Equal(7, input.Value);

        for (var i = 0; i < 6; i++) Key(ctx, Keys.Down);
        Assert.Equal(3, input.Value);
    }

    [Fact]
    public void FractionalStep_ProducesCleanValues()
    {
        var (ctx, _, input) = NewInput(0.5);
        input.Step = 0.1;
        ctx.Focus(input);

        Key(ctx, Keys.Up);
        Key(ctx, Keys.Up);
        Key(ctx, Keys.Up);

        Assert.Equal(0.8, input.Value);
        Assert.Equal("0.8", input.Text); // no binary float noise in the text
    }

    [Fact]
    public void Buttons_StepFocusAndDontMoveCaret()
    {
        var (ctx, root, input) = NewInput(10);

        Click(ctx, root, PlusX, ButtonY);
        Assert.Equal(11, input.Value);
        Assert.Same(input, ctx.FocusedElement); // spinner click focuses the field

        Click(ctx, root, MinusX, ButtonY);
        Click(ctx, root, MinusX, ButtonY);
        Assert.Equal(9, input.Value);
        Assert.False(input.HasSelection); // press consumed by the button, not the caret
    }

    [Fact]
    public void RapidButtonClicks_StepTwiceWithoutFlashingASelection()
    {
        var (ctx, root, input) = NewInput(10);

        ctx.UpdatePointer(root, PlusX, ButtonY, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, PlusX, ButtonY, false, false, true, Vector2.Zero);
        // Second press lands in the double-click window; the button must consume it.
        ctx.UpdatePointer(root, PlusX, ButtonY, true, true, false, Vector2.Zero);
        Assert.False(input.HasSelection); // no one-frame selection flash
        ctx.UpdatePointer(root, PlusX, ButtonY, false, false, true, Vector2.Zero);

        Assert.Equal(12, input.Value);
    }

    [Fact]
    public void StepFromEmpty_StartsFromLastValue()
    {
        var (ctx, root, input) = NewInput(5);
        input.AllowNull = true;
        ctx.Focus(input);
        Key(ctx, Keys.A, KeyModifiers.Control);
        Key(ctx, Keys.Backspace);

        Click(ctx, root, PlusX, ButtonY);
        Assert.Equal(6, input.Value);
    }

    [Fact]
    public void CaretScroll_ReservesButtonSpace()
    {
        // Inner width 88 minus 40 of buttons = 48 px of text viewport = 5 chars.
        var (ctx, root, input) = NewInput(width: 100);
        ctx.Focus(input);
        Type(ctx, "1234567890"); // 96 px of text
        root.Layout();

        Assert.Equal(96 - 48, input.ScrollX, 2);
    }
}
