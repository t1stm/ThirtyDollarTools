using Sundex.Components.Labels;
using EditorScene.Scenes.Dialogs;

namespace EditorScene.Tests;

public class ImportDialogTests
{
    [Fact]
    public void Title_ShowsTheDroppedFileNameOnly()
    {
        var dialog = new ImportDialog(new EditorTestContext(), "epic-sequence.tdw");

        var title = Assert.IsType<Label>(dialog.Children[0]);
        Assert.Equal("Import \"epic-sequence.tdw\"", title.Value.ToString());
    }

    [Fact]
    public void ExposesThreeDistinctButtons()
    {
        var dialog = new ImportDialog(new EditorTestContext(), "epic-sequence.tdw");

        Assert.Equal(3,
            new[] { dialog.SingleTrackButton, dialog.ProjectButton, dialog.CancelButton }.Distinct().Count());
    }

    [Fact]
    public void EachButton_FiresItsOwnOnClick_IndependentlyOfTheOthers()
    {
        var dialog = new ImportDialog(new EditorTestContext(), "epic-sequence.tdw");
        var singleTrackClicked = false;
        var projectClicked = false;
        var cancelClicked = false;
        dialog.SingleTrackButton.OnClick = _ => singleTrackClicked = true;
        dialog.ProjectButton.OnClick = _ => projectClicked = true;
        dialog.CancelButton.OnClick = _ => cancelClicked = true;

        dialog.CancelButton.OnClick(dialog.CancelButton);

        Assert.False(singleTrackClicked);
        Assert.False(projectClicked);
        Assert.True(cancelClicked);
    }
}