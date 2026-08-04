using EditorScene.Scenes.Dialogs;

namespace EditorScene.Tests;

public class UnsavedChangesDialogTests
{
    [Fact]
    public void ExposesThreeDistinctButtons()
    {
        var dialog = new UnsavedChangesDialog(new EditorTestContext());

        Assert.Equal(3, new[] { dialog.SaveButton, dialog.DiscardButton, dialog.CancelButton }.Distinct().Count());
    }

    [Fact]
    public void EachButton_FiresItsOwnOnClick_IndependentlyOfTheOthers()
    {
        var dialog = new UnsavedChangesDialog(new EditorTestContext());
        var saveClicked = false;
        var discardClicked = false;
        var cancelClicked = false;
        dialog.SaveButton.OnClick = _ => saveClicked = true;
        dialog.DiscardButton.OnClick = _ => discardClicked = true;
        dialog.CancelButton.OnClick = _ => cancelClicked = true;

        dialog.DiscardButton.OnClick(dialog.DiscardButton);

        Assert.False(saveClicked);
        Assert.True(discardClicked);
        Assert.False(cancelClicked);
    }
}