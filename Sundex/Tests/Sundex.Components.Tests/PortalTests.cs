using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class PortalTests
{
    [Fact]
    public void ChildrenGoToTheOverlayHost_AndEndWithThePortal()
    {
        var ctx = new TestUIContext();
        var host = new FlexPanel(ctx);
        ctx.OverlayHost = host;
        var page = new FlexPanel(ctx);
        var portal = new Portal(ctx);
        page.AddChild(portal);
        var dialog = new Label(ctx, "dialog");

        portal.AddChild(dialog);

        Assert.Same(host, dialog.Parent);
        Assert.Empty(portal.Children);
        portal.Dispose();
        Assert.True(dialog.IsDisposed);
        Assert.Empty(host.Children);
    }

    [Fact]
    public void WithNoHost_ItIsARegion()
    {
        var ctx = new TestUIContext();
        var portal = new Portal(ctx);
        var label = new Label(ctx, "here");
        portal.AddChild(label);
        Assert.Same(portal, label.Parent);
    }
}
