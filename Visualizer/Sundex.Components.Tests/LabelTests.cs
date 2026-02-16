using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Xunit;

namespace Sundex.Components.Tests;

public class LabelTests
{
    [Fact]
    public void TestLabelCreationWithoutTextProvider()
    {
        var context = new TestUIContext();
        // Clear the text provider to test the safe path
        var field = typeof(UIContext).GetField("_textProvider", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        field?.SetValue(null, null);

        var label = new Label(context, "Test");
        
        Assert.Equal("Test", label.Value.ToString());
        Assert.Equal(0, label.Width);
        Assert.Equal(0, label.Height);
    }

    [Fact]
    public void TestLabelPropertiesWithoutTextProvider()
    {
        var context = new TestUIContext();
        var field = typeof(UIContext).GetField("_textProvider", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        field?.SetValue(null, null);

        var label = new Label(context, "Test");
        label.FontSizePx = 24;
        label.SetTextContents("New Text");
        
        Assert.Equal(24, label.FontSizePx);
        Assert.Equal("New Text", label.Value.ToString());
    }
}
