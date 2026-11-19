using OpenTK.Mathematics;
using Sundex.Core.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;
using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Components.Tests;

public class ButtonTests
{
    [Fact]
    public void TestButtonInitialization()
    {
        var context = new TestUIContext();
        var button = new Button(context, "Click Me");

        Assert.Equal("Click Me", button.Value.ToString());
        Assert.True(button.Width.Auto);
        Assert.True(button.Height.Auto);
    }

    [Fact]
    public void TestButtonValueChange()
    {
        var context = new TestUIContext();
        var button = new Button(context, "Old Value")
        {
            Value = "New Value"
        };

        Assert.Equal("New Value", button.Value.ToString());
    }

    [Fact]
    public void TestButtonOnClick()
    {
        var context = new TestUIContext();
        var clicked = false;
        var button = new Button(context, "Click Me")
        {
            OnClick = _ => clicked = true
        };

        // We can't easily simulate MouseState to trigger Test(mouse), 
        // but we can manually trigger OnClick for this test.
        button.OnClick?.Invoke(button);

        Assert.True(clicked);
    }

    [Fact]
    public void Disabled_SwallowsTheClickAndTakesTheDisabledState()
    {
        var context = new TestUIContext();
        var root = new Panel(context) { Width = 400, Height = 300 };
        var outerClicks = 0;
        var clicks = 0;
        var button = new Button(context, "Import") { X = 10, Y = 10, Width = 100, Height = 30, OnClick = _ => clicks++ };
        root.OnClick = _ => outerClicks++;
        root.Children = [button];
        root.Layout();

        var holder = new StyleSheetHolder();
        holder.Components[button.Tag] = new Dictionary<string, IStyleValue>
        {
            ["cursor"] = new StringValue("Pointer"),
            ["state[disabled]"] = new BlockValue(new Dictionary<string, IStyleValue> { ["cursor"] = new StringValue("Default") })
        };
        button.ApplyStyleSheet(new StyleSheet(holder));

        button.Disabled = true;
        Assert.Equal(UIState.Disabled, button.CurrentState);
        Assert.Equal(CursorType.Default, button.Cursor);

        context.UpdatePointer(root, 20, 20, true, true, false, Vector2.Zero);
        context.UpdatePointer(root, 20, 20, false, false, true, Vector2.Zero);
        Assert.Equal(0, clicks);
        Assert.Equal(0, outerClicks);

        button.Disabled = false;
        Assert.Equal(CursorType.Pointer, button.Cursor);
        context.UpdatePointer(root, 20, 20, true, true, false, Vector2.Zero);
        context.UpdatePointer(root, 20, 20, false, false, true, Vector2.Zero);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void TestButtonBorderRadiusPropagation()
    {
        var context = new TestUIContext();
        var button = new Button(context, "Rounded")
        {
            BorderRadius = 15,
            // Emulate styling a background
            Background = new ColoredPlane { Color = Vector4.One }
        };

        button.Layout();

        var background = button.Background as IBorderRadius;
        Assert.NotNull(background);
        Assert.Equal(15, background.BorderRadius);
    }

    [Fact]
    public void AStateOverrideTakesTheLastClassThatSetsIt()
    {
        // Bug 2.7: base styles let the last class win, state overrides took the first.
        var context = new TestUIContext();
        var button = new Button(context, "Import") { Width = 100, Height = 30 };
        button.Classes.Add("base");
        button.Classes.Add("modifier");

        static BlockValue Disabled(string cursor) =>
            new(new Dictionary<string, IStyleValue> { ["cursor"] = new StringValue(cursor) });

        var holder = new StyleSheetHolder();
        holder.Classes["base"] = new Dictionary<string, IStyleValue> { ["state[disabled]"] = Disabled("Default") };
        holder.Classes["modifier"] = new Dictionary<string, IStyleValue> { ["state[disabled]"] = Disabled("Text") };
        button.ApplyStyleSheet(new StyleSheet(holder));

        button.Disabled = true;
        Assert.Equal(CursorType.Text, button.Cursor);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ADisabledOpacityLeavesWithTheDisabledState(bool hovered, bool restyled)
    {
        // The studio's Lead vocal: enabled again, it stayed at its disabled 0.4.
        var context = new TestUIContext();
        var root = new Panel(context) { Width = 400, Height = 300 };
        var button = new Button(context, "Lead vocal") { X = 10, Y = 10, Width = 100, Height = 30 };
        root.Children = [button];
        root.Layout();

        var holder = new StyleSheetHolder();
        holder.Components[button.Tag] = new Dictionary<string, IStyleValue>
        {
            ["state[disabled]"] = new BlockValue(new Dictionary<string, IStyleValue> { ["opacity"] = new NumberValue(0.4f, "") })
        };
        button.ApplyStyleSheet(new StyleSheet(holder));
        if (hovered) context.UpdatePointer(root, 20, 20, false, false, false, Vector2.Zero);

        button.Disabled = true;
        Assert.Equal(0.4f, button.Opacity, 3);
        if (restyled) button.SetClass("stem", true);
        button.Disabled = false;
        Assert.Equal(1f, button.Opacity, 3);
    }
}
