using System.Reflection;
using OpenTK.Mathematics;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine.Text;

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

    [Fact]
    public void SetTextContents_GrowingPastCapacity_KeepsRenderingAtTheCorrectPosition()
    {
        // Growing past the slice's capacity disposes it and gets a replacement, whose
        // constructor renders once at (0,0,0). The label's position has to be carried over and
        // re-rendered, or the text lands in the GPU buffer away from where the label sits.
        var context = new TestUIContext();
        var root = new Panel(context) { Width = 800, Height = 600 };
        var label = new Label(context, "Hi") { X = 50, Y = 30 };
        root.Children = [label];
        root.Layout();

        var before = GetTextSlicePosition(label);
        Assert.Equal(new Vector3(50, 30, 0), before);

        label.SetTextContents("Hello there"); // longer than "Hi" -> dispose+recreate path

        Assert.Equal(before, GetTextSlicePosition(label));
    }

    [Fact]
    public void SetTextContents_WithoutFontSizePxEverSet_DoesNotZeroTheFontSize()
    {
        // SetTextContents resolves FontSizePx into TextSlice.FontSize on every call, so a
        // label that never set FontSizePx must keep TextSlice's own default of 16 rather than
        // take a zero size (which renders nothing while Computed bounds still look correct).
        var context = new TestUIContext();
        var label = new Label(context, "Hi");

        label.SetTextContents("Hello there");

        Assert.NotEqual(0, GetTextSliceFontSize(label));
    }

    private static Vector3 GetTextSlicePosition(Label label)
    {
        return GetTextSlice(label).Position;
    }

    private static float GetTextSliceFontSize(Label label)
    {
        return GetTextSlice(label).FontSize;
    }

    private static TextSlice GetTextSlice(Label label)
    {
        var property = typeof(Label).GetProperty("TextSlice", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (TextSlice)property.GetValue(label)!;
    }
}