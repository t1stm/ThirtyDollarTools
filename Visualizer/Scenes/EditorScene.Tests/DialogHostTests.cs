using EditorScene.Scenes.Dialogs;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace EditorScene.Tests;

public class DialogHostTests
{
    [Fact]
    public void TryConfirmTop_RunsTheTopDialogsPrimaryAction()
    {
        var ctx = new EditorTestContext();
        var host = new DialogHost(ctx, new Panel(ctx));
        var under = 0;
        var top = 0;
        host.Show(new Label(ctx, "under"), () => under++);
        host.Show(new Label(ctx, "top"), () => top++);

        Assert.True(host.TryConfirmTop());

        Assert.Equal(1, top);
        Assert.Equal(0, under);
    }

    [Fact]
    public void TryConfirmTop_DoesNothing_WithoutADialogOrWithoutAPrimaryAction()
    {
        var ctx = new EditorTestContext();
        var host = new DialogHost(ctx, new Panel(ctx));

        Assert.False(host.TryConfirmTop());

        host.Show(new Label(ctx, "no primary action"));
        Assert.False(host.TryConfirmTop());
    }

    [Fact]
    public void Confirm_TakesEnterAsConfirm_AndEscAsCancel()
    {
        var ctx = new EditorTestContext();
        var host = new DialogHost(ctx, new Panel(ctx));
        var confirmed = 0;
        var cancelled = 0;

        host.Confirm("Delete this?", () => confirmed++, () => cancelled++);
        Assert.True(host.TryConfirmTop());
        Assert.Equal((1, 0), (confirmed, cancelled));
        Assert.False(host.HasOpenModal);

        host.Confirm("Delete this?", () => confirmed++, () => cancelled++);
        Assert.True(host.TryCloseTop());
        Assert.Equal((1, 1), (confirmed, cancelled));
        Assert.False(host.HasOpenModal);
    }

    [Fact]
    public void TryKeyTop_HandsTheKeyToTheTopDialog()
    {
        var ctx = new EditorTestContext();
        var host = new DialogHost(ctx, new Panel(ctx));
        Assert.False(host.TryKeyTop(new KeyboardKeyEventArgs(Keys.Down, 0, 0, false)));

        host.ShowFileDialog("Open project", null, ".tdwproj", _ => { });

        // The file dialog's list takes the arrows; a key it has no use for falls through.
        Assert.True(host.TryKeyTop(new KeyboardKeyEventArgs(Keys.Down, 0, 0, false)));
        Assert.False(host.TryKeyTop(new KeyboardKeyEventArgs(Keys.F5, 0, 0, false)));
    }

    /// <summary>A markup dialog can't override HandleKeyDown, so it hands its keys in at Show.</summary>
    [Fact]
    public void TryKeyTop_OffersTheKeyToTheHandlerGivenAtShow()
    {
        var ctx = new EditorTestContext();
        var host = new DialogHost(ctx, new Panel(ctx));
        var keys = new List<Keys>();
        host.Show(new Label(ctx, "keys"), onKey: e =>
        {
            keys.Add(e.Key);
            return e.Key == Keys.D1;
        });

        Assert.True(host.TryKeyTop(new KeyboardKeyEventArgs(Keys.D1, 0, 0, false)));
        Assert.False(host.TryKeyTop(new KeyboardKeyEventArgs(Keys.F5, 0, 0, false)));
        Assert.Equal([Keys.D1, Keys.F5], keys);
    }
}
