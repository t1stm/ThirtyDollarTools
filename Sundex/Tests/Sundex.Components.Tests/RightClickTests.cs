using OpenTK.Mathematics;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class RightClickTests
{
    [Fact]
    public void ARightClickFiresOnceAndBubblesToTheNearestListener()
    {
        var ctx = new TestUIContext();
        var root = new FlexPanel(ctx) { Width = 200, Height = 200 };
        var row = new FlexPanel(ctx) { Width = 100, Height = 40 };
        var inner = new FlexPanel(ctx) { Width = 20, Height = 20 };
        row.AddChild(inner);
        root.AddChild(row);
        root.Layout();
        var clicks = 0;
        row.OnRightClick = _ => clicks++;

        ctx.UpdatePointer(root, 5, 5, false, false, false, Vector2.Zero, isRightDown: true);
        ctx.UpdatePointer(root, 5, 5, false, false, false, Vector2.Zero, isRightDown: true);
        ctx.UpdatePointer(root, 5, 5, false, false, false, Vector2.Zero);
        ctx.UpdatePointer(root, 5, 5, false, false, false, Vector2.Zero, isRightDown: true);

        Assert.Equal(2, clicks);
    }
}
