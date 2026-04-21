using Sundex.Components.Labels;

namespace Sundex.Components.Tests;

public class LabelTests
{
    [Fact]
    public void TestLabelCreationWithMockTextProvider()
    {
        var context = new TestUIContext();
        var label = new Label(context, "Test");

        Assert.Equal("Test", label.Value.ToString());
        Assert.NotEqual(0, label.Width);
        Assert.NotEqual(0, label.Height);
    }

    [Fact]
    public void TestLabelPropertiesWithMockTextProvider()
    {
        var context = new TestUIContext();
        var label = new Label(context, "Test")
        {
            FontSizePx = 24
        };
        label.SetTextContents("New Text");

        Assert.Equal(24, label.FontSizePx.Value);
        Assert.Equal("New Text", label.Value.ToString());
    }
}