using EditorScene.Scenes.Dialogs;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Tests;

public class ImportWarningsDialogTests
{
    private static readonly ImportWarnings Everything = new(
        new Dictionary<string, int> { ["!divider"] = 92, ["!flash"] = 83 }, 11, ["mystery"]);

    [Fact]
    public void PianoRollImport_ShowsEverySection_AndOffersFaithful()
    {
        var dialog = new ImportWarningsDialog(new EditorTestContext(), "song.tdw", ImportMode.Track, Everything);
        var text = Texts(dialog.Element);

        Assert.Contains("Imported \"song.tdw\" as a Piano Roll track", text);
        Assert.Contains("!divider", text);
        Assert.Contains("x92", text);
        Assert.Contains("mystery", text);
        Assert.Contains("Not in the sample set, so they were left out.", text);
        Assert.Contains("11 notes moved to the nearest step.", text);
        Assert.True(dialog.OffersFaithful);
        Assert.Contains(dialog.FaithfulButton, Walk(dialog.Element));
        Assert.Equal("Keep Piano Roll", dialog.KeepButton.Label.Value.ToString());
        // Faithful is the suggested shape for a TDW sequence, so it holds the fill.
        Assert.Contains("dialog-button-primary", dialog.FaithfulButton.Classes);
        Assert.DoesNotContain("dialog-button-primary", dialog.KeepButton.Classes);
    }

    [Fact]
    public void ProjectImport_HasNoFaithfulWayBack()
    {
        var dialog = new ImportWarningsDialog(new EditorTestContext(), "song.tdw", ImportMode.Project, Everything);

        Assert.False(dialog.OffersFaithful);
        Assert.DoesNotContain(dialog.FaithfulButton, Walk(dialog.Element));
        Assert.Equal("OK", dialog.KeepButton.Label.Value.ToString());
        Assert.Contains("dialog-button-primary", dialog.KeepButton.Classes);
    }

    [Fact]
    public void UnknownSoundsAlone_ShowOnlyTheirSection_AndNoFaithfulOffer()
    {
        // A faithful track can't place an unknown sound either, so offering one would promise
        // something it doesn't do.
        var warnings = new ImportWarnings(new Dictionary<string, int>(), 0, ["mystery"]);
        var dialog = new ImportWarningsDialog(new EditorTestContext(), "song.tdw", ImportMode.Track, warnings);
        var text = Texts(dialog.Element);

        Assert.DoesNotContain("Left out", text);
        Assert.DoesNotContain("Snapped to the grid", text);
        Assert.Contains("Unknown sounds", text);
        Assert.False(dialog.OffersFaithful);
    }

    [Fact]
    public void FaithfulImport_SaysUnknownSoundsBecomeSilentSteps()
    {
        var warnings = new ImportWarnings(new Dictionary<string, int>(), 0, ["mystery"]);
        var dialog = new ImportWarningsDialog(new EditorTestContext(), "song.tdw", ImportMode.Faithful, warnings);

        Assert.Contains("Not in the sample set, so they play as silent steps.", Texts(dialog.Element));
    }

    [Fact]
    public void OneQuantizedNote_IsSingular()
    {
        var warnings = new ImportWarnings(new Dictionary<string, int>(), 1, []);
        var dialog = new ImportWarningsDialog(new EditorTestContext(), "song.tdw", ImportMode.Track, warnings);

        Assert.Contains("1 note moved to the nearest step.", Texts(dialog.Element));
    }

    [Fact]
    public void LongLists_AreCutToFit_AndCountTheRest()
    {
        // Two rows of events, one of sounds - the minimum window height is the budget.
        var warnings = new ImportWarnings(
            Enumerable.Range(0, 10).ToDictionary(i => $"!e{i}", _ => 1), 0,
            [.. Enumerable.Range(0, 11).Select(i => $"sound{i}")]);
        var dialog = new ImportWarningsDialog(new EditorTestContext(), "song.tdw", ImportMode.Track, warnings);
        var text = Texts(dialog.Element);

        Assert.Contains("!e7", text);
        Assert.DoesNotContain("!e8", text);
        Assert.Contains("and 2 more", text);
        Assert.Contains("sound3", text);
        Assert.DoesNotContain("sound4", text);
        Assert.Contains("and 7 more", text);
    }

    private static List<string> Texts(UIElement root) =>
        [.. Walk(root).OfType<Label>().Select(label => label.Value.ToString())];

    private static IEnumerable<UIElement> Walk(UIElement element)
    {
        yield return element;
        if (element is not Panel panel) yield break;
        foreach (var child in panel.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }
}
