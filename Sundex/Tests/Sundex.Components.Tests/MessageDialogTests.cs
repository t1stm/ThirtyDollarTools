using OpenTK.Mathematics;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class MessageDialogTests
{
    [Fact]
    public void ShowsAndDismissesViaButton()
    {
        var ctx = new TestUIContext();
        var root = new Panel(ctx) { Width = 800, Height = 600 };

        var modal = MessageDialog.Show(ctx, root, "Line one\nLine two");
        root.Layout();
        Assert.Contains(modal, root.Children);

        var content = (Panel)modal.Children[0];
        var button = content.Children.OfType<Labels.Button>().Single();
        var x = button.Computed.AbsoluteX + 2;
        var y = button.Computed.AbsoluteY + 2;
        ctx.UpdatePointer(root, x, y, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, x, y, false, false, true, Vector2.Zero);

        Assert.DoesNotContain(modal, root.Children);
    }
}
