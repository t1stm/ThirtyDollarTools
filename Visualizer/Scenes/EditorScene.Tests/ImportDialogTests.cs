using EditorScene.Scenes.Dialogs;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Tests;

public class ImportDialogTests
{
    private static readonly Vector4[] Palette = [new(1, 0, 0, 1), new(0, 1, 0, 1)];

    private static ImportDialog NewDialog(string text = "kick|snare|!divider|kick", bool dirty = false)
    {
        var ctx = new EditorTestContext();
        var sequence = Sequence.FromString(text);
        var sounds = new Dictionary<string, Sound>
        {
            ["kick"] = new() { Id = "kick" },
            ["snare"] = new() { Id = "snare" }
        };
        return new ImportDialog(ctx, ctx.NewAtlasStore(), "epic-sequence.tdw", sequence,
            SequenceImporter.Preview(sequence, sounds), sounds, Palette, "Untitled Project", dirty);
    }

    private static KeyboardKeyEventArgs Key(Keys key, KeyModifiers modifiers = 0) => new(key, 0, modifiers, false);

    private static IEnumerable<UIElement> Walk(UIElement element)
    {
        yield return element;
        if (element is not Panel panel) yield break;
        foreach (var child in panel.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    // A label's text keeps its slice's length, padded with NULs once it gets shorter.
    private static string Text(Label label) => label.Value.ToString().TrimEnd('\0');

    private static string Action(ImportDialog dialog) => Text(dialog.ImportButton.Label);

    private static List<string> Texts(ImportDialog dialog) =>
        [.. Walk(dialog.Element).OfType<Label>().Select(Text)];

    [Fact]
    public void Header_IsImport_WithTheFileNameBesideIt()
    {
        var texts = Texts(NewDialog());

        Assert.Contains("Import", texts);
        Assert.Contains("epic-sequence", texts); // the base name; the chip says what it is
        Assert.Contains("TDW sequence", texts);
    }

    [Fact]
    public void StartsOnFaithful_WithItsAction_AndTheFileNameAsTheName()
    {
        var dialog = NewDialog();

        Assert.Equal(ImportMode.Faithful, dialog.Mode);
        Assert.Equal("Import as Faithful Track", Action(dialog));
        Assert.Contains("dialog-button-primary", dialog.ImportButton.Classes);
        Assert.Equal("epic-sequence", dialog.NameInput.Value);
    }

    [Fact]
    public void SummarisesTheFile_UnderTheList()
    {
        var texts = Texts(NewDialog());

        Assert.Contains("4 events, 2 sounds.", texts);
        Assert.Contains("Starts at 300 BPM.", texts);
    }

    [Fact]
    public void NumberKeys_PickTheirWay_AndUpDownStepThroughThem()
    {
        var dialog = NewDialog();

        Assert.True(dialog.HandleKey(Key(Keys.D3)));
        Assert.Equal(ImportMode.Project, dialog.Mode);
        Assert.True(dialog.HandleKey(Key(Keys.D2)));
        Assert.Equal(ImportMode.Track, dialog.Mode);
        Assert.True(dialog.HandleKey(Key(Keys.Up)));
        Assert.Equal(ImportMode.Faithful, dialog.Mode);
        Assert.True(dialog.HandleKey(Key(Keys.Up)));
        Assert.Equal(ImportMode.Faithful, dialog.Mode);
        Assert.True(dialog.HandleKey(Key(Keys.Down)));
        Assert.True(dialog.HandleKey(Key(Keys.Down)));
        Assert.True(dialog.HandleKey(Key(Keys.Down)));
        Assert.Equal(ImportMode.Project, dialog.Mode);

        Assert.False(dialog.HandleKey(Key(Keys.D1, KeyModifiers.Control)));
        Assert.False(dialog.HandleKey(Key(Keys.A)));
        Assert.Equal(ImportMode.Project, dialog.Mode);
    }

    [Fact]
    public void ClickingARow_PicksItsWay_AndMarksIt()
    {
        var dialog = NewDialog();
        var row = Walk(dialog.Element).Single(element => element.ID == "piano-roll-row");

        row.OnClick!(row);

        Assert.Equal(ImportMode.Track, dialog.Mode);
        Assert.Contains("track-kind-row-selected", row.Classes);
        Assert.DoesNotContain("track-kind-row-idle", row.Classes);
    }

    [Theory]
    [InlineData(ImportMode.Faithful, "Import as Faithful Track")]
    [InlineData(ImportMode.Track, "Import as Piano Roll")]
    [InlineData(ImportMode.Project, "Replace project")]
    public void TheAction_NamesThePickedWay(ImportMode mode, string action)
    {
        var dialog = NewDialog();
        dialog.Mode = ImportMode.Project; // the longest label first, so a shorter one has to replace it
        dialog.Mode = mode;

        Assert.Equal(action, Action(dialog));
    }

    [Fact]
    public void Replace_IsTheRedButton_WithADarkLabel_AndBackToBlueAfter()
    {
        var dialog = NewDialog();

        dialog.Mode = ImportMode.Project;
        Assert.Contains("dialog-button-danger", dialog.ImportButton.Classes);
        Assert.DoesNotContain("dialog-button-primary", dialog.ImportButton.Classes);
        Assert.Equal("key-button", dialog.ImportButton.Classes[^1]);
        Assert.Contains("dark-label", dialog.ImportButton.Label.Classes);

        dialog.Mode = ImportMode.Faithful;
        Assert.Contains("dialog-button-primary", dialog.ImportButton.Classes);
        Assert.DoesNotContain("dialog-button-danger", dialog.ImportButton.Classes);
        Assert.Equal("key-button", dialog.ImportButton.Classes[^1]);
        Assert.DoesNotContain("dark-label", dialog.ImportButton.Label.Classes);
    }

    [Fact]
    public void EachWay_StatesWhatItLoses()
    {
        var dialog = NewDialog("kick|snare|!divider|mystery|kick");

        Assert.Contains("1 unknown sound kept as silent steps: mystery", Texts(dialog));
        dialog.Mode = ImportMode.Track;
        Assert.Contains("1 action and 1 unknown sound left out.", Texts(dialog));
        dialog.Mode = ImportMode.Project;
        Assert.Contains("1 action and 1 unknown sound left out.", Texts(dialog));
        Assert.Contains("2 Piano Roll tracks, one per sound.", Texts(dialog));
    }

    [Theory]
    [InlineData(false, "Replaces Untitled Project.")]
    [InlineData(true, "Replaces Untitled Project. Unsaved changes are lost.")]
    public void Replace_WarnsOfUnsavedChanges_OnlyWhenThereAreAny(bool dirty, string fact)
    {
        var dialog = NewDialog(dirty: dirty);

        dialog.Mode = ImportMode.Project;

        Assert.Contains(fact, Texts(dialog));
    }

    [Fact]
    public void AFileTheGridCannotTake_SaysSo_OnTheGridWays()
    {
        var dialog = NewDialog("!speed@200|!divider");

        dialog.Mode = ImportMode.Track;

        // One problem, then what to do instead - not two warnings.
        Assert.Contains("Can't go on a grid: no sounds found in this sequence.", Texts(dialog));
        Assert.Contains("Import it as a Faithful track instead.", Texts(dialog));
        Assert.DoesNotContain(Texts(dialog), text => text.StartsWith("Starts at"));
    }

    [Fact]
    public void AWayThatCantImport_IsMarked_AndItsButtonIsDisabled()
    {
        var dialog = NewDialog("!speed@200|!divider");
        Assert.True(dialog.CanImport); // Faithful takes anything

        dialog.Mode = ImportMode.Track;

        Assert.False(dialog.CanImport);
        Assert.True(dialog.ImportButton.Disabled);
        Assert.Equal(2, Texts(dialog).Count(text => text == "Not available for this file")); // Piano Roll and Replace

        dialog.Mode = ImportMode.Faithful;
        Assert.False(dialog.ImportButton.Disabled);
    }

    [Fact]
    public void AFileWithNothingKnownInIt_SaysItIsntASequence()
    {
        var dialog = NewDialog("hello world");

        Assert.Contains("This doesn't look like a TDW sequence.", Texts(dialog));
    }
}
