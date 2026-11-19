using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

/// <summary>The text features Label gained in the fork. The mock glyph is 0.6 em: 9.6 px at 16 px.</summary>
public class LabelFeatureTests
{
    private static (FlexPanel Column, Label Label) InColumn(string text, float width, Action<Label> setup)
    {
        var context = new TestUIContext();
        var column = new FlexPanel(context)
        {
            Width = width, Height = 500, Direction = LayoutDirection.Vertical, AlignItems = Align.Stretch
        };
        var label = new Label(context, text);
        setup(label);
        column.Children = [label];
        column.Layout();
        return (column, label);
    }

    [Fact]
    public void AWrappingLabelBreaksToTheWidthItIsGiven()
    {
        var (_, label) = InColumn("aaa bbb ccc", 80, target => target.Wrap = true);

        Assert.Equal("aaa bbb\nccc", label.Shown);
        Assert.Equal(2, label.LineCount);
        Assert.Equal(80, label.Computed.Width);
    }

    [Fact]
    public void ATruncatedLabelEndsInAnEllipsis()
    {
        var (_, label) = InColumn("abcdefghijklmnop", 50, target => target.Truncate = true);

        Assert.EndsWith("…", label.Shown);
        Assert.Equal(1, label.LineCount);
        Assert.Equal("abcdefghijklmnop", label.Text);
    }

    [Fact]
    public void LineClampStopsAfterThatManyLines()
    {
        var (_, label) = InColumn("aaa bbb ccc ddd eee", 40, target => target.LineClamp = 2);

        Assert.Equal(2, label.LineCount);
        Assert.EndsWith("…", label.Shown);
    }

    [Fact]
    public void UppercaseChangesWhatIsShownNotTheText()
    {
        var (_, label) = InColumn("quiet", 300, target => target.TextTransform = TextTransform.Uppercase);

        Assert.Equal("QUIET", label.Shown);
        Assert.Equal("quiet", label.Text);
    }

    [Fact]
    public void LetterSpacingWidensTheText()
    {
        var context = new TestUIContext();
        var plain = new Label(context, "abcd");
        var spaced = new Label(context, "abcd") { LetterSpacing = 0.25f };

        var plainWidth = plain.Measure(1000, 1000).width;
        var spacedWidth = spaced.Measure(1000, 1000).width;

        Assert.Equal(plainWidth + 4 * 0.25f * 16, spacedWidth, 3);
    }

    [Fact]
    public void LineHeightSetsTheHeightOfEachLine()
    {
        var context = new TestUIContext();
        var label = new Label(context, "one\ntwo") { LineHeight = 1.5f };

        Assert.Equal(2 * 1.5f * 16, label.Measure(1000, 1000).height, 3);
    }

    [Fact]
    public void OpacityIsInheritedFromTheParentAndAppliedToTheColour()
    {
        var context = new TestUIContext();
        var panel = new FlexPanel(context);
        var label = new Label(context, "x");
        panel.AddChild(label);

        panel.Opacity = 0.5f;
        label.Opacity = 0.5f;

        Assert.Equal(0.25f, label.EffectiveOpacity, 3);
        Assert.Equal(1f, label.Color.W, 3);
    }

    [Fact]
    public void DisposingAPanelDisposesWhatIsInIt()
    {
        var context = new TestUIContext();
        var panel = new FlexPanel(context);
        var label = new Label(context, "x");
        panel.AddChild(label);
        var root = new FlexPanel(context);
        root.AddChild(panel);

        panel.Dispose();

        Assert.True(label.IsDisposed);
        Assert.Empty(root.Children);
        Assert.Null(panel.Parent);
    }
}
