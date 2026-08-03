using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class ModalLayerTests
{
    private static (TestUIContext ctx, Panel root, ModalLayer modal, Panel content) NewModal()
    {
        var ctx = new TestUIContext();
        var root = new Panel(ctx) { Width = 800, Height = 600 };
        var modal = new ModalLayer(ctx);
        var content = new Panel(ctx) { Width = 100, Height = 100, Background = new ColoredPlane() };
        modal.AddChild(content);
        root.AddChild(modal);
        root.Layout();
        return (ctx, root, modal, content);
    }

    private static void Click(TestUIContext ctx, Panel root, float x, float y)
    {
        ctx.UpdatePointer(root, x, y, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, x, y, false, false, true, Vector2.Zero);
    }

    [Fact]
    public void BlocksClicksToElementsBeneath()
    {
        var ctx = new TestUIContext();
        var root = new Panel(ctx) { Width = 800, Height = 600 };
        var clicked = 0;
        var button = new Panel(ctx) { X = 10, Y = 10, Width = 100, Height = 30, OnClick = _ => clicked++ };
        root.AddChild(button);
        root.Layout();

        Click(ctx, root, 50, 20);
        Assert.Equal(1, clicked); // reachable without the modal

        root.AddChild(new ModalLayer(ctx));
        root.Layout();
        Click(ctx, root, 50, 20);
        Assert.Equal(1, clicked); // modal absorbs it
    }

    [Fact]
    public void BackdropClick_FiresDismiss_ContentClickDoesNot()
    {
        var (ctx, root, modal, _) = NewModal();
        var dismissed = 0;
        modal.OnDismissRequested = _ => dismissed++;

        Click(ctx, root, 400, 300); // content is centered at (350,250)-(450,350)
        Assert.Equal(0, dismissed);

        Click(ctx, root, 50, 20); // backdrop
        Assert.Equal(1, dismissed);
    }

    [Fact]
    public void ConsumesScroll()
    {
        var (_, _, modal, _) = NewModal();
        Assert.True(modal.HandleScroll(new Vector2(0, -1)));
    }

    [Fact]
    public void IndexStaysPinnedToTopLayer()
    {
        var (_, _, modal, content) = NewModal();
        Assert.Equal(ModalLayer.TopLayerIndex, modal.Index); // parenting didn't clobber it
        Assert.Equal(ModalLayer.TopLayerIndex + 1, content.Index);
    }
}