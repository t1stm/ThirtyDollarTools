using EditorScene.Scenes;
using EditorScene.Scenes.Dialogs;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Inputs;
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
    public void Show_TakesFocusFromAFieldBehindTheModal_ButNotFromOneInsideIt()
    {
        var ctx = new EditorTestContext();
        var root = new Panel(ctx);
        var host = new DialogHost(ctx, root);
        var behind = new TextInput(ctx);
        root.AddChild(behind);

        ctx.Focus(behind);
        host.Show(new Label(ctx, "dialog"));
        Assert.Null(ctx.FocusedElement);

        var inside = new TextInput(ctx);
        var content = new FlexPanel(ctx) { Children = [inside] };
        ctx.Focus(inside);
        host.Show(content);
        Assert.Same(inside, ctx.FocusedElement);
    }

    [Fact]
    public void Show_WiresTheCloseButtonToTheModalsDismiss_EvenOneSetAfterwards()
    {
        var ctx = new EditorTestContext();
        var host = new DialogHost(ctx, new Panel(ctx));
        var close = new Button(ctx, "x") { ID = "close-button" };
        var content = new FlexPanel(ctx) { Children = [new FlexPanel(ctx) { Children = [close] }] };
        var dismissed = 0;

        var modal = host.Show(content);
        modal.OnDismissRequested = _ => dismissed++;
        close.OnClick!(close);

        Assert.Equal(1, dismissed);
    }

    /// <summary>
    ///     The documents still without the shared header, each until its own rework. Remove a
    ///     dialog from here when it gets one; a new dialog never belongs here.
    /// </summary>
    private static readonly string[] HeaderPending =
        ["Sound_Filter", "Import_Warnings_Dialog", "Confirm_Dialog", "Instrument_Editor"];

    public static TheoryData<string> DialogDocuments() =>
    [
        .. typeof(EditorInterface).Assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Dialogs.") && n.EndsWith(".snx.xml") && !HeaderPending.Any(n.Contains))
    ];

    /// <summary>A dialog's × is what lets a mouse user out; DialogHost.Show wires it by id.</summary>
    [Theory]
    [MemberData(nameof(DialogDocuments))]
    public void EveryDialogDocument_OpensWithAHeaderAndACloseButton(string resource)
    {
        using var stream = typeof(EditorInterface).Assembly.GetManifestResourceStream(resource)!;
        var xml = new StreamReader(stream).ReadToEnd();

        Assert.Contains("class=\"dialog-header\"", xml);
        Assert.Contains("id=\"close-button\"", xml);
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
