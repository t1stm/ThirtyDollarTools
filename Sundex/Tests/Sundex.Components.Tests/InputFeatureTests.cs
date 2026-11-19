using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine;

namespace Sundex.Components.Tests;

/// <summary>What TextInput and Button gained in the fork: clipboard, undo, password, length limit, keyboard press.</summary>
public class InputFeatureTests
{
    private static (TestUIContext Context, TextInput Input) Focused(string text = "")
    {
        var context = new TestUIContext();
        var root = new Panel(context) { Width = 800, Height = 600 };
        var input = new TextInput(context, text) { Width = 300 };
        root.Children = [input];
        root.Layout();
        context.Focus(input);
        return (context, input);
    }

    private static void Type(TestUIContext context, string text)
    {
        foreach (var character in text) context.DispatchTextInput(new TextInputEventArgs(character));
    }

    private static void Key(TestUIContext context, Keys key, KeyModifiers modifiers = 0) =>
        context.DispatchKeyDown(new KeyboardKeyEventArgs(key, 0, modifiers, false));

    [Fact]
    public void CutAndPasteGoThroughTheClipboard()
    {
        var (context, input) = Focused("hello world");
        Key(context, Keys.A, KeyModifiers.Control);
        Key(context, Keys.X, KeyModifiers.Control);
        Assert.Equal("", input.Value);
        Assert.Equal("hello world", Clipboard.Read());

        Key(context, Keys.V, KeyModifiers.Control);
        Key(context, Keys.V, KeyModifiers.Control);
        Assert.Equal("hello worldhello world", input.Value);
    }

    [Fact]
    public void UndoTakesBackATypingRunAtOnceAndRedoPutsItBack()
    {
        var (context, input) = Focused();
        Type(context, "abc");
        Type(context, " ");
        Type(context, "def");
        Assert.Equal("abc def", input.Value);

        Key(context, Keys.Z, KeyModifiers.Control);
        Assert.Equal("abc ", input.Value);
        Key(context, Keys.Z, KeyModifiers.Control);
        Assert.Equal("abc", input.Value);
        Key(context, Keys.Z, KeyModifiers.Control);
        Assert.Equal("", input.Value);

        Key(context, Keys.Z, KeyModifiers.Control | KeyModifiers.Shift);
        Assert.Equal("abc", input.Value);
    }

    [Fact]
    public void APasswordShowsDotsAndCannotBeCopied()
    {
        var (context, input) = Focused();
        input.Password = true;
        Clipboard.Write("before");
        Type(context, "secret");

        var label = input.Children.OfType<Label>().First();
        Assert.Equal("••••••", label.Shown);
        Assert.Equal("secret", input.Value);

        Key(context, Keys.A, KeyModifiers.Control);
        Key(context, Keys.C, KeyModifiers.Control);
        Assert.Equal("before", Clipboard.Read());
    }

    [Fact]
    public void MaxLengthStopsTypingAndPasting()
    {
        var (context, input) = Focused();
        input.MaxLength = 4;
        Type(context, "abcdef");
        Assert.Equal("abcd", input.Value);

        Clipboard.Write("xyz");
        Key(context, Keys.Backspace);
        Key(context, Keys.V, KeyModifiers.Control);
        Assert.Equal("abcx", input.Value);
    }

    [Fact]
    public void SettingTheSameValueKeepsTheSelection()
    {
        var (context, input) = Focused("keep");
        Key(context, Keys.A, KeyModifiers.Control);

        input.Value = "keep";

        Assert.True(input.HasSelection);
    }

    [Fact]
    public void EnterAndSpacePressAFocusedButton()
    {
        var context = new TestUIContext();
        var root = new Panel(context) { Width = 800, Height = 600 };
        var button = new Button(context);
        var presses = 0;
        button.OnClick = _ => presses++;
        root.Children = [button];
        root.Layout();
        context.Focus(button);

        Key(context, Keys.Enter);
        Key(context, Keys.Space);

        Assert.Equal(2, presses);
        Assert.Equal(UIState.Focused, button.CurrentState);
    }
}
