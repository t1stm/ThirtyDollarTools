using Sundex.Components.Panels;
using Sundex.Components.Tests.Layouts.BasicStack;
using Sundex.Components.Tests.Layouts.NestedFlex;
using Sundex.Components.Tests.Layouts.WindowLayout;

namespace Sundex.Components.Tests.Layouts;

public class LayoutTests
{
    private readonly TestUIContext _context = new();

    [Fact]
    public void TestBasicStackLayout()
    {
        var component = BasicStackLayout.Create(_context);
        var layout = (StackPanel)component.Element;
        layout.Layout();

        Assert.Equal(200, layout.Computed.Width);
        Assert.Equal(400, layout.Computed.Height);
        Assert.Equal(3, layout.Children.Count);

        // Padding = 20, Spacing = 10
        // Child 1: AbsoluteX = layout.AbsoluteX + 20
        Assert.Equal(20, layout.Children[0].Computed.AbsoluteX - layout.Computed.AbsoluteX);
        Assert.Equal(20, layout.Children[0].Computed.AbsoluteY - layout.Computed.AbsoluteY);

        Assert.Equal(20, layout.Children[1].Computed.AbsoluteX - layout.Computed.AbsoluteX);
        Assert.Equal(20 + 50 + 10, layout.Children[1].Computed.AbsoluteY - layout.Computed.AbsoluteY);

        Assert.Equal(20, layout.Children[2].Computed.AbsoluteX - layout.Computed.AbsoluteX);
        Assert.Equal(20 + 50 + 10 + 50 + 10, layout.Children[2].Computed.AbsoluteY - layout.Computed.AbsoluteY);
    }

    [Fact]
    public void TestNestedFlexLayout()
    {
        var component = NestedFlexLayout.Create(_context);
        var layout = (FlexPanel)component.Element;
        layout.Layout();

        Assert.Equal(500, layout.Computed.Width);
        Assert.Equal(500, layout.Computed.Height);
        Assert.Equal(2, layout.Children.Count);

        var row1 = layout.Children[0];
        var row2 = layout.Children[1];

        Assert.Equal(480,
            row1.Computed
                .Width); // 500 - 2*10 padding (Wait, is it 2*Padding or just Padding? Checked previous work, it's inner_width = Computed.Width - 2 * Padding;)
        Assert.Equal(100, row1.Computed.Height);

        Assert.Equal(480, row2.Computed.Width);
        Assert.Equal(100, row2.Computed.Height);
    }

    [Fact]
    public void TestWindowLayout()
    {
        var component = WindowLayoutMock.Create(_context);
        var layout = (Panel)component.Element;
        layout.Layout();

        Assert.Equal(400, layout.Computed.Width);
        Assert.Equal(300, layout.Computed.Height);

        Assert.Single(layout.Children);
        var stack = (StackPanel)layout.Children[0];

        Assert.Equal(2, stack.Children.Count);
    }
}