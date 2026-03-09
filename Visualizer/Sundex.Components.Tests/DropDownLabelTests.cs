using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class DropDownLabelTests
{
    [Fact]
    public void TestDropDownLabelInitialization()
    {
        var context = new TestUIContext();
        var children = new List<UIElement> { new Panel(context) };
        var dropDown = new DropDownLabel(context, "Menu", children);

        Assert.Equal("Menu", dropDown.Value.ToString());
        Assert.False(dropDown.Panel.Visible);
        Assert.Equal(2, dropDown.Children.Count); // Label and Panel
    }

    [Fact]
    public void TestDropDownLabelToggle()
    {
        var context = new TestUIContext();
        var children = new List<UIElement> { new Panel(context) };
        var dropDown = new DropDownLabel(context, "Menu", children);

        // Simulate click on label
        dropDown.Label.OnClick?.Invoke(dropDown.Label);
        Assert.True(dropDown.Panel.Visible);

        dropDown.Label.OnClick?.Invoke(dropDown.Label);
        Assert.False(dropDown.Panel.Visible);
    }
}