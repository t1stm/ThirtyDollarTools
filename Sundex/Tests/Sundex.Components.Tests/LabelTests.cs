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
        // Regression test: growing past the slice's capacity disposes it and gets a
        // replacement, whose constructor renders once at the default (0,0,0) position
        // before this label's actual on-screen position is ever applied to it. Without
        // carrying the position over and re-rendering, the text is written to the GPU
        // buffer at the wrong spot - effectively invisible where the label actually sits.
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
        // Regression: FontSizePx used to default to LiteralOrComputable's zero value, and
        // SetTextContents unconditionally resolves it into TextSlice.FontSize on every
        // call. A label that never explicitly sets FontSizePx (e.g. EditorInterface's
        // instrument button) rendered fine at construction - that first render uses
        // TextSlice's own hardcoded default of 16 - then went invisible (FontSize=0) the
        // moment its text was ever updated, even though its Computed bounds (and thus
        // clicks) stayed correct.
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