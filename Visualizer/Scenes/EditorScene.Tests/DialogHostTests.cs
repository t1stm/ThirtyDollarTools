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

        host.Confirm("Delete this?", "It goes for good.", () => confirmed++, () => cancelled++);
        Assert.True(host.TryConfirmTop());
        Assert.Equal((1, 0), (confirmed, cancelled));
        Assert.False(host.HasOpenModal);

        host.Confirm("Delete this?", "It goes for good.", () => confirmed++, () => cancelled++);
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

    public static TheoryData<string> DialogDocuments() =>
    [
        .. typeof(EditorInterface).Assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Dialogs.") && n.EndsWith(".snx.xml"))
    ];

    /// <summary>A dialog's × is what lets a mouse user out; DialogHost.Show wires it by id.</summary>
    [Theory]
    [MemberData(nameof(DialogDocuments))]
    public void EveryDialogDocument_OpensWithAHeaderAndACloseButton(string resource)
    {
        using var stream = typeof(EditorInterface).Assembly.GetManifestResourceStream(resource)!;
        var xml = new StreamReader(stream).ReadToEnd();

        // The instrument editor's first row is its name field, with the × at its end.
        if (!resource.Contains("Instrument_Editor")) Assert.Contains("class=\"dialog-header\"", xml);
        Assert.Contains("id=\"close-button\"", xml);
    }

    [Fact]
    public void Alert_ClosesOnEnter_AndItsCopyButtonCopiesTheWholeError()
    {
        var ctx = new EditorTestContext();
        var root = new Panel(ctx);
        var host = new DialogHost(ctx, root);
        string? copied = null;
        host.CopyText = text => copied = text;

        host.Alert("Couldn't open broken.tdwproj", "This file isn't a project.", "'n' is an invalid JSON literal.");
        var copy = Descendants(root).OfType<Button>().Single(b => b.Label.Value.ToString() == "Copy error");
        copy.OnClick!(copy);

        Assert.Equal("Couldn't open broken.tdwproj\nThis file isn't a project.\n\n'n' is an invalid JSON literal.", copied);
        Assert.Equal("Copied", copy.Label.Value.ToString().TrimEnd('\0'));
        Assert.True(host.TryConfirmTop());
        Assert.False(host.HasOpenModal);
    }

    [Fact]
    public void Alert_WithoutDetails_HasNoCopyButton()
    {
        var ctx = new EditorTestContext();
        var root = new Panel(ctx);
        var host = new DialogHost(ctx, root);

        host.Alert("Couldn't convert “Track 3”", "No sounds found in this sequence.");

        Assert.DoesNotContain(Descendants(root).OfType<Button>(), b => b.Label.Value.ToString() == "Copy error");
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException), false, "boom", false)]
    [InlineData(typeof(System.Text.Json.JsonException), false, "This file isn't a project the editor can read.", true)]
    [InlineData(typeof(DirectoryNotFoundException), true, "That folder doesn't exist. Pick another one.", true)]
    [InlineData(typeof(UnauthorizedAccessException), true, "The editor isn't allowed to write there.", true)]
    [InlineData(typeof(NotSupportedException), false, "The editor ran into an error", true)]
    public void Explain_SaysWhatHappenedInPlainWords(Type type, bool writing, string start, bool keepsDetails)
    {
        var error = (Exception)Activator.CreateInstance(type, "boom")!;

        var (message, details) = DialogHost.Explain(error, writing);

        Assert.StartsWith(start, message);
        Assert.Equal(keepsDetails ? "boom" : null, details);
    }

    private static IEnumerable<Sundex.Components.Abstractions.UIElement> Descendants(Sundex.Components.Abstractions.UIElement element)
    {
        yield return element;
        if (element is not Panel panel) yield break;
        foreach (var child in panel.Children)
        foreach (var descendant in Descendants(child))
            yield return descendant;
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
