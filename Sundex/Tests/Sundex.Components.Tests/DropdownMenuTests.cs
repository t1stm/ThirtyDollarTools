using OpenTK.Mathematics;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class DropdownMenuTests
{
    private const float ViewportWidth = 1920;
    private const float ViewportHeight = 1080;

    private static (TestUIContext ctx, Panel root, DropdownMenu menu) Open(float x, float y)
    {
        var ctx = new TestUIContext();
        var root = new Panel(ctx) { Width = ViewportWidth, Height = ViewportHeight };
        var menu = new DropdownMenu(ctx, x, y);
        menu.AddItem("Duplicate", () => { });
        menu.AddItem("Delete", () => { });
        root.AddChild(menu);
        root.Layout();
        return (ctx, root, menu);
    }

    private static void Click(TestUIContext ctx, Panel root, float x, float y)
    {
        ctx.UpdatePointer(root, x, y, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, x, y, false, false, true, Vector2.Zero);
    }

    [Fact]
    public void OpensToTheRightAndBelowTheCursor()
    {
        var (_, _, menu) = Open(100, 100);
        Assert.True(menu.Menu.Computed.AbsoluteX > 100);
        Assert.True(menu.Menu.Computed.AbsoluteY >= 100);
    }

    [Fact]
    public void FlipsLeftWhenItWouldOverflowTheRightEdge()
    {
        var (_, _, menu) = Open(1915, 100);
        var box = menu.Menu.Computed;
        Assert.True(box.AbsoluteX + box.Width <= ViewportWidth);
        Assert.True(box.AbsoluteX < 1915);
    }

    [Fact]
    public void FlipsUpWhenItWouldOverflowTheBottomEdge()
    {
        var (_, _, menu) = Open(100, ViewportHeight - 5);
        var box = menu.Menu.Computed;
        Assert.True(box.AbsoluteY + box.Height <= ViewportHeight);
    }

    [Fact]
    public void MenuWiderThanTheViewportClampsToTheLeftEdge()
    {
        var ctx = new TestUIContext();
        var root = new Panel(ctx) { Width = ViewportWidth, Height = ViewportHeight };
        var menu = new DropdownMenu(ctx, 900, 100);
        menu.Menu.Width = ViewportWidth + 500;
        menu.AddItem("Wide", () => { });
        root.AddChild(menu);
        root.Layout();

        Assert.Equal(0, menu.Menu.Computed.AbsoluteX);
    }

    [Fact]
    public void ClickOutsideClosesTheMenu()
    {
        var (ctx, root, menu) = Open(100, 100);
        Click(ctx, root, 800, 800);
        Assert.DoesNotContain(menu, root.Children);
    }

    [Fact]
    public void ClickingAnItemRunsTheActionAndCloses()
    {
        var ctx = new TestUIContext();
        var root = new Panel(ctx) { Width = ViewportWidth, Height = ViewportHeight };
        var menu = new DropdownMenu(ctx, 100, 100);
        var ran = 0;
        var item = menu.AddItem("Duplicate", () => ran++);
        root.AddChild(menu);
        root.Layout();

        Click(ctx, root, item.Computed.AbsoluteX + item.Computed.Width / 2,
            item.Computed.AbsoluteY + item.Computed.Height / 2);

        Assert.Equal(1, ran);
        Assert.DoesNotContain(menu, root.Children);
    }

    [Fact]
    public void HeldOpeningRightPressDoesNotCloseIt()
    {
        var ctx = new TestUIContext();
        var root = new Panel(ctx) { Width = ViewportWidth, Height = ViewportHeight };
        // The press that opens the menu: level-triggered, so it keeps firing while held.
        ctx.UpdatePointer(root, 100, 100, false, false, false, Vector2.Zero, true);

        var menu = new DropdownMenu(ctx, 100, 100);
        menu.AddItem("Duplicate", () => { });
        root.AddChild(menu);
        root.Layout();

        ctx.UpdatePointer(root, 100, 100, false, false, false, Vector2.Zero, true);
        Assert.Contains(menu, root.Children);

        // A fresh right-press outside the box does close it.
        ctx.UpdatePointer(root, 800, 800, false, false, false, Vector2.Zero, false);
        ctx.UpdatePointer(root, 800, 800, false, false, false, Vector2.Zero, true);
        Assert.DoesNotContain(menu, root.Children);
    }

    [Fact]
    public void BelowAnElement_HangsFlushUnderItsBottomLeft()
    {
        var ctx = new TestUIContext();
        var root = new Panel(ctx) { Width = ViewportWidth, Height = ViewportHeight };
        var button = new Panel(ctx) { X = 200, Y = 300, Width = 120, Height = 32 };
        root.AddChild(button);
        root.Layout();

        var menu = DropdownMenu.Below(button);
        menu.AddItem("Hermite", () => { });
        root.AddChild(menu);
        root.Layout();

        Assert.Equal(200, menu.Menu.Computed.AbsoluteX);
        Assert.Equal(332, menu.Menu.Computed.AbsoluteY); // flush under it, no cursor gap
    }

    [Fact]
    public void BelowAnElement_FlipsToItsOwnEdgesNearTheViewportEdge()
    {
        var ctx = new TestUIContext();
        var root = new Panel(ctx) { Width = ViewportWidth, Height = ViewportHeight };
        var button = new Panel(ctx)
            { X = ViewportWidth - 120, Y = ViewportHeight - 32, Width = 120, Height = 32 };
        root.AddChild(button);
        root.Layout();

        var menu = DropdownMenu.Below(button);
        menu.AddItem("Hermite", () => { });
        menu.Menu.Width = 240;
        root.AddChild(menu);
        root.Layout();

        var box = menu.Menu.Computed;
        // Right-aligned to the button, not shoved a button's width off it.
        Assert.Equal(ViewportWidth, box.AbsoluteX + box.Width);
        // And above it, not overlapping it.
        Assert.Equal(ViewportHeight - 32, box.AbsoluteY + box.Height);
    }

    [Fact]
    public void ItemsStretchToTheMenuWidth()
    {
        var (_, _, menu) = Open(100, 100);
        var inner = menu.Menu.Computed.Width - 2 * menu.Menu.Padding;
        foreach (var item in menu.Menu.Children)
            Assert.Equal(inner, item.Computed.Width, 3);
    }
}
