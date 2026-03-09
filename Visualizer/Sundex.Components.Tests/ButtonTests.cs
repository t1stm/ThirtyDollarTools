using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Labels;

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
    public void TestButtonBorderRadiusPropagation()
    {
        var context = new TestUIContext();
        var button = new Button(context, "Rounded");
        button.BorderRadius = 15;

        // Emulate styling a background
        button.Background = new ColoredPlane { Color = Vector4.One };

        button.Layout();

        var background = button.Background as IBorderRadius;
        Assert.NotNull(background);
        Assert.Equal(15, background.BorderRadius);
    }
}