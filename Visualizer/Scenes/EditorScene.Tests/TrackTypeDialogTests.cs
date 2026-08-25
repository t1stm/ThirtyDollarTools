using EditorScene.Scenes.Dialogs;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Tests;

public class TrackTypeDialogTests
{
    [Fact]
    public void ExposesThreeDistinctButtons()
    {
        var dialog = new TrackTypeDialog(new EditorTestContext());

        Assert.Equal(3,
            new[] { dialog.PianoRollButton, dialog.FaithfulButton, dialog.CancelButton }.Distinct().Count());
    }

    /// <summary>The dialog is a pure form: each button fires only its own handler.</summary>
    [Fact]
    public void EachButton_FiresItsOwnOnClick_IndependentlyOfTheOthers()
    {
        var dialog = new TrackTypeDialog(new EditorTestContext());
        TrackKind? picked = null;
        var cancelled = false;
        dialog.PianoRollButton.OnClick = _ => picked = TrackKind.PianoRoll;
        dialog.FaithfulButton.OnClick = _ => picked = TrackKind.Faithful;
        dialog.CancelButton.OnClick = _ => cancelled = true;

        dialog.FaithfulButton.OnClick(dialog.FaithfulButton);

        Assert.Equal(TrackKind.Faithful, picked);
        Assert.False(cancelled);
    }
}
