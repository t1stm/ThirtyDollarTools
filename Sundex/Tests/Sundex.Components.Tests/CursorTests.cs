using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Components.Tests;

public class CursorTests
{
    [Fact]
    public void TestCursor_BaseStyleApplied()
    {
        var context = new TestUIContext();
        var element = new TestElement(context);

        var holder = new StyleSheetHolder();
        holder.Components[element.Tag] = new Dictionary<string, IStyleValue>
        {
            ["cursor"] = new StringValue("Pointer")
        };
        var styleSheet = new StyleSheet(holder);

        element.ApplyStyleSheet(styleSheet);
        Assert.Equal(CursorType.Pointer, element.Cursor);
    }

    [Fact]
    public void TestCursor_StateOverrideApplied()
    {
        var context = new TestUIContext();
        var element = new TestElement(context);

        var holder = new StyleSheetHolder();
        var hoverProps = new Dictionary<string, IStyleValue>
        {
            ["cursor"] = new StringValue("ResizeX")
        };

        holder.Components[element.Tag] = new Dictionary<string, IStyleValue>
        {
            ["cursor"] = new StringValue("Pointer"),
            ["state[hovered]"] = new BlockValue(hoverProps)
        };
        var styleSheet = new StyleSheet(holder);

        element.ApplyStyleSheet(styleSheet);
        Assert.Equal(CursorType.Pointer, element.Cursor); // Base style

        // Let's use reflection to set CurrentState for the test since its setter is private.
        var prop = typeof(UIElement).GetProperty("CurrentState");
        prop!.SetValue(element, UIState.Hovered);

        Assert.Equal(CursorType.ResizeX, element.Cursor);

        prop.SetValue(element, UIState.None);
        Assert.Equal(CursorType.Pointer, element.Cursor);
    }

    [Fact]
    public void TestCursor_RequestCursorCalled()
    {
        var requestedCursor = CursorType.Default;
        var context = new TestUIContext
        {
            RequestCursor = c => requestedCursor = c
        };

        var element = new TestElement(context);
        element.Cursor = CursorType.ResizeY;
        element.IsHovered = true;

        element.Update(context);

        Assert.Equal(CursorType.ResizeY, requestedCursor);
    }

    /// <summary>
    ///     Hovering a spinner button hovers the field too (the hover chain includes
    ///     ancestors), so the deeper element must be the one that wins the cursor.
    /// </summary>
    [Fact]
    public void TestCursor_NumericInputButtonsWinOverTheField()
    {
        var requestedCursor = CursorType.Default;
        var context = new TestUIContext
        {
            RequestCursor = c => requestedCursor = c
        };

        var input = new NumericInput(context, 1) { IsHovered = true };
        input.Update(context);
        Assert.Equal(CursorType.Text, requestedCursor);

        // The two spinner buttons are the last children (see NumericInput's ctor).
        input.Children[^1].IsHovered = true;
        input.Update(context);
        Assert.Equal(CursorType.Pointer, requestedCursor);
    }

    private class TestElement : UIElement
    {
        public TestElement(UIContext context) : base(context)
        {
        }

        public override string Tag => "test-element";

        protected override void DrawSelf(UIContext context)
        {
        }
    }
}