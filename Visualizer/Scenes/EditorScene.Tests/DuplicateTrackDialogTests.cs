using EditorScene.Scenes.Dialogs;

namespace EditorScene.Tests;

public class DuplicateTrackDialogTests
{
    [Fact]
    public void Name_IsTheTypedName_OrTheSuggestionWhenTheFieldIsCleared()
    {
        var dialog = new DuplicateTrackDialog(new EditorTestContext(), "Drums", "Drums copy");
        Assert.Equal("Drums copy", dialog.Name);

        dialog.NameInput.Value = "  Fills  ";
        Assert.Equal("Fills", dialog.Name);

        dialog.NameInput.Value = "   ";
        Assert.Equal("Drums copy", dialog.Name);
    }

    [Fact]
    public void EnterInTheNameField_Duplicates()
    {
        var dialog = new DuplicateTrackDialog(new EditorTestContext(), "Drums", "Drums copy");
        var duplicated = 0;
        dialog.DuplicateButton.OnClick = _ => duplicated++;

        dialog.NameInput.OnCommit!(dialog.NameInput);

        Assert.Equal(1, duplicated);
    }
}
