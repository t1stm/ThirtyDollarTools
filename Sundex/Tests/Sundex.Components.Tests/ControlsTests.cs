using OpenTK.Mathematics;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class ControlsTests
{
    [Fact]
    public void ASwitchFlipsOnClick_AndColoursItsTrack()
    {
        var ctx = new TestUIContext();
        var root = new FlexPanel(ctx) { Width = 200, Height = 100 };
        var toggle = new Switch(ctx);
        root.AddChild(toggle);
        root.Layout();
        var changes = 0;
        toggle.OnValueChanged = _ => changes++;

        ctx.UpdatePointer(root, 10, 10, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, 10, 10, false, false, true, Vector2.Zero);

        Assert.True(toggle.Value);
        Assert.Equal(1, changes);
        Assert.Equal(toggle.OnFill, toggle.Fill);

        toggle.Disabled = true;
        ctx.UpdatePointer(root, 10, 10, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, 10, 10, false, false, true, Vector2.Zero);
        Assert.True(toggle.Value);
    }

    [Fact]
    public void ASegmentedControlMarksAndPicksItsSegments()
    {
        var ctx = new TestUIContext();
        var segmented = new Segmented(ctx) { Options = [("a", "Alpha"), ("b", "Beta")], Value = "a" };
        var buttons = segmented.Children.OfType<Button>().ToList();
        Assert.Equal(2, buttons.Count);
        Assert.Contains("picked", buttons[0].Classes);

        buttons[1].OnClick!(buttons[1]);

        Assert.Equal("b", segmented.Value);
        Assert.DoesNotContain("picked", buttons[0].Classes);
        Assert.Contains("picked", buttons[1].Classes);
    }

    [Fact]
    public void DetailsShowsItsBodyWhenTheSummaryIsClicked()
    {
        var ctx = new TestUIContext();
        var details = new Details(ctx);
        var summary = new Label(ctx, "More");
        var body = new Label(ctx, "Body");
        details.AddChild(summary);
        details.AddChild(body);
        Assert.False(body.Visible);

        summary.OnClick!(summary);
        Assert.True(details.Open);
        Assert.True(body.Visible);
    }
}
