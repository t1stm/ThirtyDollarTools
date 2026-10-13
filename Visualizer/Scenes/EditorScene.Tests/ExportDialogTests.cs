using EditorScene.Scenes.Dialogs;

namespace EditorScene.Tests;

public class ExportDialogTests
{
    [Fact]
    public void Defaults_AreTheLibraryOnes_ExceptTheDividerSpacingTheFormPicks()
    {
        var dialog = new ExportDialog(new EditorTestContext());

        var style = dialog.Style;
        // The one deliberate divergence: SequenceStyle defaults dividers off (null), but a
        // human reading an exported .tdw wants sections, so the form pre-fills 2 bars (same
        // choice BMS2TDWex makes). The rest is seeded from `new SequenceStyle()`.
        Assert.Equal(2, style.DividerEveryBars);
        Assert.False(style.DividerOnSpeedChanges);
        Assert.Equal(8, style.MigrateToStop);
    }

    [Fact]
    public void EditedForm_BuildsTheMatchingStyle()
    {
        var dialog = new ExportDialog(new EditorTestContext())
        {
            DividerEveryBars =
            {
                Value = 4
            },
            DividerOnSpeedChanges =
            {
                Checked = true
            },
            MigrateToStop =
            {
                Value = null // empty = never use "!stop"
            }
        };

        var style = dialog.Style;
        Assert.Equal(4, style.DividerEveryBars);
        Assert.True(style.DividerOnSpeedChanges);
        Assert.Null(style.MigrateToStop);
    }

    [Fact]
    public void PickingAudio_ShowsItsLine_AndNamesTheExportAfterIt()
    {
        var dialog = new ExportDialog(new EditorTestContext());
        var options = (Sundex.Components.Panels.Panel)dialog.Element.Children
            .Single(child => child.ID == "export-options");
        Assert.Equal("Export .tdw…", dialog.ExportButton.Value.ToString());
        Assert.Equal("tdw-options", Assert.Single(options.Children).ID);

        dialog.Format = ExportFormat.Wav;

        Assert.Equal("Export .wav…", dialog.ExportButton.Value.ToString());
        Assert.Equal("wav-options", Assert.Single(options.Children).ID);

        dialog.Format = ExportFormat.Tdw;
        Assert.Equal("tdw-options", Assert.Single(options.Children).ID);
    }
}
