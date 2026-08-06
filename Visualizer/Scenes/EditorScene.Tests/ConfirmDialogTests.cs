using EditorScene.Scenes.Dialogs;

namespace EditorScene.Tests;

public class ConfirmDialogTests
{
    [Fact]
    public void MultilineMessage_FitsWithinTheDialogsInnerWidth()
    {
        // No Styled() call: a dialog document carries its own <style>, so it is styled as
        // it is built rather than by a host's cascade.
        var dialog = new ConfirmDialog(new EditorTestContext(),
            "Delete \"Layer\"?\nThis removes it from every note\nthat uses it.");
        dialog.Element.Layout();

        var message = dialog.Element.Children[0];
        var innerWidth = dialog.Element.Width.Value - 2 * dialog.Element.Padding;
        Assert.True(message.Computed.Width <= innerWidth,
            $"message width {message.Computed.Width} overflows the dialog's inner width {innerWidth}");
    }

    [Fact]
    public void ConfirmButton_HasARoundedCorner()
    {
        var dialog = new ConfirmDialog(new EditorTestContext(), "Delete this?");

        Assert.True(dialog.ConfirmButton.BorderRadius.Value > 0);
    }
}