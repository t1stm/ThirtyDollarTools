using EditorScene.Scenes.Components;

namespace EditorScene.Tests;

public class ExportDialogTests
{
    [Fact]
    public void Defaults_MatchTheLibraryDefaults()
    {
        var dialog = new ExportDialog(new EditorTestContext());

        var style = dialog.Style;
        Assert.Equal(0, style.DividerEveryBars); // 0 = off (SequenceStyle treats < 1 as off)
        Assert.False(style.DividerOnSpeedChanges);
        Assert.Equal(8, style.MigrateToStop);
    }

    [Fact]
    public void EditedForm_BuildsTheMatchingStyle()
    {
        var dialog = new ExportDialog(new EditorTestContext());

        dialog.DividerEveryBars.Value = 4;
        dialog.DividerOnSpeedChanges.Checked = true;
        dialog.MigrateToStop.Value = null; // empty = never use "!stop"

        var style = dialog.Style;
        Assert.Equal(4, style.DividerEveryBars);
        Assert.True(style.DividerOnSpeedChanges);
        Assert.Null(style.MigrateToStop);
    }
}
