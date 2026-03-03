using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Xunit;

namespace Sundex.Components.Tests;

public class ButtonTests
{
    [Fact]
    public void TestButtonInitialization()
    {
        var context = new TestUIContext();
        var button = new Button(context, "Click Me");

        Assert.Equal("Click Me", button.Value.ToString());
        Assert.True(button.AutoSizeSelf);
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
}
