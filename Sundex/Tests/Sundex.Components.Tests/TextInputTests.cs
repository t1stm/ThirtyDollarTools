using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Inputs;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

// The MockGlyphProvider advances 0.6 units/char; at font size 16 every char is 9.6 px wide.
public class TextInputTests
{
    private const float CharWidth = 9.6f;

    private static (TestUIContext ctx, Panel root, TextInput input) NewInput(float width = 200)
    {
        var ctx = new TestUIContext();
        var root = new Panel(ctx) { Width = 800, Height = 600 };
        var input = new TextInput(ctx) { X = 10, Y = 10, Width = width };
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

    [Fact]
    public void Typing_InsertsAtCaretAndFiresValueChanged()
    {
        var (ctx, _, input) = NewInput();
        var changes = 0;
        input.OnValueChanged = _ => changes++;

        ctx.Focus(input);
        Type(ctx, "hello");

        Assert.Equal("hello", input.Value);
        Assert.Equal(5, input.CaretIndex);
        Assert.Equal(5, changes);

        // Move the caret and insert in the middle.
        Key(ctx, Keys.Left);
        Key(ctx, Keys.Left);
        Type(ctx, "X");
        Assert.Equal("helXlo", input.Value);
        Assert.Equal(4, input.CaretIndex);
    }

    [Fact]
    public void UnfocusedInput_ReceivesNothing()
    {
        var (ctx, _, input) = NewInput();
        Type(ctx, "hello");
        Assert.Equal("", input.Value);
    }

    [Fact]
    public void BackspaceAndDelete_Work()
    {
        var (ctx, _, input) = NewInput();
        ctx.Focus(input);
        Type(ctx, "abc");

        Key(ctx, Keys.Backspace);
        Assert.Equal("ab", input.Value);

        Key(ctx, Keys.Home);
        Key(ctx, Keys.Delete);
        Assert.Equal("b", input.Value);
    }

    [Fact]
    public void ShiftArrows_SelectAndTypingReplacesSelection()
    {
        var (ctx, _, input) = NewInput();
        ctx.Focus(input);
        Type(ctx, "abcdef");

        Key(ctx, Keys.Left, KeyModifiers.Shift);
        Key(ctx, Keys.Left, KeyModifiers.Shift);
        Assert.True(input.HasSelection);
        Assert.Equal(4, input.SelectionStart);
        Assert.Equal(6, input.SelectionEnd);

        Type(ctx, "X");
        Assert.Equal("abcdX", input.Value);
        Assert.False(input.HasSelection);
    }

    [Fact]
    public void CtrlA_SelectsAll_BackspaceClears()
    {
        var (ctx, _, input) = NewInput();
        ctx.Focus(input);
        Type(ctx, "abcdef");

        Key(ctx, Keys.A, KeyModifiers.Control);
        Assert.Equal(0, input.SelectionStart);
        Assert.Equal(6, input.SelectionEnd);

        Key(ctx, Keys.Backspace);
        Assert.Equal("", input.Value);
    }

    [Fact]
    public void ArrowWithoutShift_CollapsesSelectionToEdge()
    {
        var (ctx, _, input) = NewInput();
        ctx.Focus(input);
        Type(ctx, "abcd");
        Key(ctx, Keys.A, KeyModifiers.Control);

        Key(ctx, Keys.Left);
        Assert.False(input.HasSelection);
        Assert.Equal(0, input.CaretIndex);

        Key(ctx, Keys.A, KeyModifiers.Control);
        Key(ctx, Keys.Right);
        Assert.Equal(4, input.CaretIndex);
    }

    [Fact]
    public void IntegerFilter_AllowsDigitsAndLeadingMinusOnly()
    {
        var (ctx, _, input) = NewInput();
        input.Filter = TextInputFilter.Integer;
        ctx.Focus(input);

        Type(ctx, "-12a.5-");
        Assert.Equal("-125", input.Value);
    }

    [Fact]
    public void DecimalFilter_AllowsSingleDot()
    {
        var (ctx, _, input) = NewInput();
        input.Filter = TextInputFilter.Decimal;
        ctx.Focus(input);

        Type(ctx, "3.1.4x");
        Assert.Equal("3.14", input.Value);
    }

    [Fact]
    public void Enter_CommitsAndBlurs()
    {
        var (ctx, _, input) = NewInput();
        string? committed = null;
        input.OnCommit = i => committed = i.Value;

        ctx.Focus(input);
        Type(ctx, "42");
        Key(ctx, Keys.Enter);

        Assert.Equal("42", committed);
        Assert.Null(ctx.FocusedElement);
    }

    [Fact]
    public void Escape_Blurs()
    {
        var (ctx, _, input) = NewInput();
        ctx.Focus(input);
        Key(ctx, Keys.Escape);
        Assert.Null(ctx.FocusedElement);
    }

    [Fact]
    public void PlainKeys_AreConsumed_ModifiedCombosAndEscapePassThrough()
    {
        var (ctx, _, input) = NewInput();
        ctx.Focus(input);

        // Space must not fall through to scene hotkeys (play/pause) mid-typing.
        Assert.True(ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.Space, 0, 0, false)));
        Assert.True(ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.Space, 0, KeyModifiers.Shift, false)));

        // App-level combos still reach the scene.
        Assert.False(input.HandleKeyDown(new KeyboardKeyEventArgs(Keys.S, 0, KeyModifiers.Control, false)));
        Assert.False(input.HandleKeyDown(new KeyboardKeyEventArgs(Keys.Escape, 0, 0, false)));
    }

    [Fact]
    public void ClickFocusesAndPositionsCaret()
    {
        var (ctx, root, input) = NewInput();
        input.Value = "abcdef";
        root.Layout();

        // Text starts at AbsoluteX(10) + Padding(6) = 16. Click inside the third char.
        var x = 16 + 2 * CharWidth + 2;
        ctx.UpdatePointer(root, x, 20, true, true, false, Vector2.Zero);

        Assert.Same(input, ctx.FocusedElement);
        Assert.Equal(2, input.CaretIndex);
        Assert.False(input.HasSelection);
    }

    [Fact]
    public void DragSelects()
    {
        var (ctx, root, input) = NewInput();
        input.Value = "abcdef";
        root.Layout();

        var start = 16 + 1 * CharWidth + 2; // inside char 1 -> caret 1
        var end = 16 + 4 * CharWidth + 2; // inside char 4 -> caret 4
        ctx.UpdatePointer(root, start, 20, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, end, 20, true, false, false, Vector2.Zero);

        Assert.Equal(1, input.SelectionStart);
        Assert.Equal(4, input.SelectionEnd);

        ctx.UpdatePointer(root, end, 20, false, false, true, Vector2.Zero);
        Assert.Equal(4, input.SelectionEnd); // release keeps the selection
    }

    [Fact]
    public void DoubleClick_SelectsTheWordUnderThePointer()
    {
        var (ctx, root, input) = NewInput();
        input.Value = "hello, brave world";
        root.Layout();

        // Text starts at x=16; click inside "brave" (chars 7..11).
        var x = 16 + 8 * CharWidth + 2;
        ctx.UpdatePointer(root, x, 20, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, x, 20, false, false, true, Vector2.Zero);
        Assert.False(input.HasSelection); // single click just places the caret

        ctx.UpdatePointer(root, x, 20, true, true, false, Vector2.Zero);
        // The button stays down for a few frames - the drag updates delivered to the
        // captured element must not collapse the word selection back to the pointer.
        ctx.UpdatePointer(root, x, 20, true, false, false, Vector2.Zero);
        ctx.UpdatePointer(root, x + 1, 20, true, false, false, Vector2.Zero);
        ctx.UpdatePointer(root, x + 1, 20, false, false, true, Vector2.Zero);
        Assert.Equal(7, input.SelectionStart);
        Assert.Equal(12, input.SelectionEnd);
        Assert.Equal(12, input.CaretIndex);

        // A fresh single press selects normally again.
        ctx.UpdatePointer(root, 16 + 2 * CharWidth, 20, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, 16 + 4 * CharWidth, 20, true, false, false, Vector2.Zero);
        Assert.Equal(2, input.SelectionStart);
        Assert.Equal(4, input.SelectionEnd);
    }

    [Fact]
    public void DoubleClick_StopsAtPunctuation()
    {
        var (ctx, root, input) = NewInput();
        input.Value = "hello, world";
        root.Layout();

        // Double-click inside "hello" (chars 0..4): the comma must not be selected.
        var x = 16 + 2 * CharWidth;
        ctx.UpdatePointer(root, x, 20, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, x, 20, false, false, true, Vector2.Zero);
        ctx.UpdatePointer(root, x, 20, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, x, 20, false, false, true, Vector2.Zero);

        Assert.Equal(0, input.SelectionStart);
        Assert.Equal(5, input.SelectionEnd);

        // Double-clicking the comma itself selects nothing.
        var commaX = 16 + 5 * CharWidth + 2;
        ctx.UpdatePointer(root, commaX, 20, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, commaX, 20, false, false, true, Vector2.Zero);
        ctx.UpdatePointer(root, commaX, 20, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, commaX, 20, false, false, true, Vector2.Zero);
        Assert.False(input.HasSelection);
    }

    [Fact]
    public void CaretScrollsIntoView()
    {
        // Inner width = 60 - 2*6 = 48 px = 5 chars.
        var (ctx, root, input) = NewInput(60);
        ctx.Focus(input);
        Type(ctx, "0123456789"); // 96 px of text
        root.Layout();

        Assert.Equal(96 - 48, input.ScrollX, 2);

        // Jump to the start: view scrolls back.
        Key(ctx, Keys.Home);
        Assert.Equal(0, input.ScrollX, 2);
    }

    [Fact]
    public void ValueSetter_StripsNewlinesAndClampsCaret()
    {
        var (ctx, _, input) = NewInput();
        ctx.Focus(input);
        Type(ctx, "abc");

        input.Value = "x\ny";
        Assert.Equal("xy", input.Value);
        Assert.True(input.CaretIndex <= input.Value.Length);
    }
}