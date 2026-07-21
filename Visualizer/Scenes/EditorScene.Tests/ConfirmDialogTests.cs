using EditorScene.Scenes.Components;

namespace EditorScene.Tests;

public class ConfirmDialogTests
{
    [Fact]
    public void MultilineMessage_FitsWithinTheDialogsInnerWidth()
    {
        var dialog = new ConfirmDialog(new EditorTestContext(),
            "Delete \"Layer\"?\nThis removes it from every note\nthat uses it.");
        dialog.Layout();

        var message = dialog.Children[0];
        Assert.True(message.Computed.Width <= dialog.Width.Value - 2 * dialog.Padding,
            $"message width {message.Computed.Width} overflows the dialog's inner width {dialog.Width.Value - 2 * dialog.Padding}");
    }

    [Fact]
    public void ConfirmButton_HasARoundedCorner()
    {
        var dialog = new ConfirmDialog(new EditorTestContext(), "Delete this?");

        Assert.True(dialog.ConfirmButton.BorderRadius.Value > 0);
    }
}
