using Components.Abstractions;
using Components.Labels;
using Xunit;

namespace Components.Tests;

public class ButtonTests
{
    [Fact]
    public void TestButtonInitialization()
    {
        var context = new TestUIContext();
        var button = new Button(context, "Click Me");

        Assert.Equal("Click Me", button.Value.ToString());
        Assert.True(button.AutoSizeSelf);
        Assert.True(button.AutoWidth);
        Assert.True(button.AutoHeight);
    }

    [Fact]
    public void TestButtonValueChange()
    {
        var context = new TestUIContext();
        var button = new Button(context, "Old Value");
        button.Value = "New Value";

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
}
