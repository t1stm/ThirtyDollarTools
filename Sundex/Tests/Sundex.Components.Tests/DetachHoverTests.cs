using OpenTK.Mathematics;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class DetachHoverTests
{
    [Fact]
    public void ARemovedHoveredElementGetsNoHoverExitAfterwards()
    {
        var ctx = new TestUIContext();
        var root = new FlexPanel(ctx) { Width = 200, Height = 200 };
        var row = new FlexPanel(ctx) { Width = 100, Height = 40 };
        root.AddChild(row);
        root.Layout();
        var exits = 0;
        row.OnHoverExit = _ => exits++;

        ctx.UpdatePointer(root, 10, 10, false, false, false, Vector2.Zero);
        Assert.True(row.IsHovered);
        root.RemoveChild(row);
        ctx.UpdatePointer(root, 150, 150, false, false, false, Vector2.Zero);

        Assert.Equal(0, exits);
        Assert.False(row.IsHovered);
    }
}
