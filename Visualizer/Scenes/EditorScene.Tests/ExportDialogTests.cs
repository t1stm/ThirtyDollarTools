using EditorScene.Scenes.Dialogs;

namespace EditorScene.Tests;

public class ExportDialogTests
{
    [Fact]
    public void OneAndTwo_PickTheFormat()
    {
        var dialog = new ExportDialog(new EditorTestContext());

        Assert.True(dialog.HandleKey(new OpenTK.Windowing.Common.KeyboardKeyEventArgs(
            OpenTK.Windowing.GraphicsLibraryFramework.Keys.D2, 0, 0, false)));
        Assert.Equal(ExportFormat.Wav, dialog.Format);
        Assert.True(dialog.HandleKey(new OpenTK.Windowing.Common.KeyboardKeyEventArgs(
            OpenTK.Windowing.GraphicsLibraryFramework.Keys.D1, 0, 0, false)));
        Assert.Equal(ExportFormat.Tdw, dialog.Format);
    }

    [Fact]
    public void TheAudioOptions_SayWhatGoesIntoTheFile()
    {
        var dialog = new ExportDialog(new EditorTestContext()) { Format = ExportFormat.Wav };

        dialog.SetTracks(3);
        dialog.SetLength(81_340);
        var texts = Texts(dialog.Element);
        Assert.Contains("3, mixed into one file", texts);
        Assert.Contains("1:21.3", texts);
        Assert.Contains("Ignored, every track plays", texts);

        dialog.SetLength(null);
        Assert.Contains("Couldn't be measured", Texts(dialog.Element));
    }

    private static List<string> Texts(Sundex.Components.Abstractions.UIElement element)
    {
        var texts = new List<string>();
        Walk(element);
        return texts;

        void Walk(Sundex.Components.Abstractions.UIElement e)
        {
            if (e is Sundex.Components.Labels.Label label) texts.Add(label.Value.ToString().TrimEnd('\0'));
            if (e is Sundex.Components.Panels.Panel panel)
                foreach (var child in panel.Children) Walk(child);
        }
    }

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
