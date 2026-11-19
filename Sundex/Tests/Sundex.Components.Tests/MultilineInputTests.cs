using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Inputs;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class MultilineInputTests
{
    private static (TestUIContext Context, TextInput Input) Focused(string text = "")
    {
        var context = new TestUIContext();
        var root = new Panel(context) { Width = 800, Height = 600 };
        var input = new TextInput(context) { Width = 300, Height = 200, Multiline = true, Value = text };
        root.Children = [input];
        root.Layout();
        context.Focus(input);
        return (context, input);
    }

    private static void Key(TestUIContext context, Keys key, KeyModifiers modifiers = 0) =>
        context.DispatchKeyDown(new KeyboardKeyEventArgs(key, 0, modifiers, false));

    [Fact]
    public void EnterStartsALine_AndTheTextKeepsIt()
    {
        var (context, input) = Focused("one");
        input.MoveCaret(3);
        Key(context, Keys.Enter);
        context.DispatchTextInput(new TextInputEventArgs('t'));
        Assert.Equal("one\nt", input.Value);
        Assert.Equal("a\nb", new TextInput(context) { Multiline = true, Value = "a\r\nb" }.Value);
    }

    [Fact]
    public void UpDownHomeAndEndMoveByLine()
    {
        var (context, input) = Focused("first\nsecond\nthird");
        input.MoveCaret(9); // "sec|ond"
        input.Layout();
        Key(context, Keys.Home);
        Assert.Equal(6, input.CaretIndex);
        Key(context, Keys.End);
        Assert.Equal(12, input.CaretIndex);
        Key(context, Keys.Up);
        Assert.True(input.CaretIndex <= 5, $"caret {input.CaretIndex} should be on the first line");
        Key(context, Keys.Down);
        Key(context, Keys.Down);
        Assert.True(input.CaretIndex >= 13, $"caret {input.CaretIndex} should be on the third line");
    }

    [Fact]
    public void AnOwnerKeyComesFirst()
    {
        var (context, input) = Focused("x");
        var taken = 0;
        input.OnKey = args => args.Control && args.Key == Keys.Enter && ++taken > 0;
        Key(context, Keys.Enter, KeyModifiers.Control);
        Assert.Equal(1, taken);
        Assert.Equal("x", input.Value);
    }
}
