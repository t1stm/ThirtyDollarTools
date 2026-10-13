using EditorScene.Scenes.Dialogs;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Tests;

public class TrackTypeDialogTests
{
    private static readonly Vector4[] Palette = [new(1, 0, 0, 1), new(0, 1, 0, 1)];

    private static TrackTypeDialog NewDialog()
    {
        var ctx = new EditorTestContext();
        return new TrackTypeDialog(ctx, ctx.NewAtlasStore(), Palette, Vector4.One);
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
    private static string Label(TrackTypeDialog dialog) => dialog.AddButton.Value.ToString().TrimEnd('\0');

    private static bool Shows(TrackTypeDialog dialog, UIElement element) => Walk(dialog.Element).Contains(element);

    [Fact]
    public void StartsOnPianoRoll_WithItsActionAndTheNameField()
    {
        var dialog = NewDialog();

        Assert.Equal(TrackKind.PianoRoll, dialog.Kind);
        Assert.Equal("Add Piano Roll", Label(dialog));
        Assert.True(Shows(dialog, dialog.NameInput));
        Assert.Null(dialog.ColorIndex);
    }

    [Fact]
    public void ClickingARow_PicksItsKind_AndNamesTheAction()
    {
        var dialog = NewDialog();
        var row = Walk(dialog.Element).Single(element => element.ID == "faithful-row");

        row.OnClick!(row);

        Assert.Equal(TrackKind.Faithful, dialog.Kind);
        Assert.Equal("Add Faithful", Label(dialog));
        Assert.Contains("track-kind-row-selected", row.Classes);
        Assert.DoesNotContain("track-kind-row-idle", row.Classes);
    }

    /// <summary>A wave track is named after its file, so its card has no name field.</summary>
    [Fact]
    public void Wave_HidesTheNameField_AndAsksForTheFileNext()
    {
        var dialog = NewDialog();

        dialog.Kind = TrackKind.Wave;

        Assert.False(Shows(dialog, dialog.NameInput));
        Assert.Equal("Choose .wav…", Label(dialog));

        dialog.Kind = TrackKind.PianoRoll;
        Assert.True(Shows(dialog, dialog.NameInput));
    }

    [Fact]
    public void NumberKeysPick_AndArrowsStepWithoutWrapping()
    {
        var dialog = NewDialog();

        Assert.True(dialog.HandleKey(Key(Keys.D3)));
        Assert.Equal(TrackKind.Wave, dialog.Kind);
        Assert.True(dialog.HandleKey(Key(Keys.Down)));
        Assert.Equal(TrackKind.Wave, dialog.Kind);
        Assert.True(dialog.HandleKey(Key(Keys.Up)));
        Assert.Equal(TrackKind.Faithful, dialog.Kind);
        Assert.True(dialog.HandleKey(Key(Keys.KeyPad1)));
        Assert.Equal(TrackKind.PianoRoll, dialog.Kind);
    }

    [Fact]
    public void OtherKeysAndModifiedOnes_AreLeftAlone()
    {
        var dialog = NewDialog();

        Assert.False(dialog.HandleKey(Key(Keys.F5)));
        Assert.False(dialog.HandleKey(Key(Keys.D2, KeyModifiers.Control)));
        Assert.Equal(TrackKind.PianoRoll, dialog.Kind);
    }

    /// <summary>The first swatch is the default fill; the rest are the palette, in order.</summary>
    [Fact]
    public void Swatches_AreTheDefaultThenThePalette()
    {
        var dialog = NewDialog();
        var swatches = Walk(dialog.Element).Where(element => element.Classes.Contains("track-swatch")).ToArray();
        Assert.Equal(Palette.Length + 1, swatches.Length);

        swatches[2].OnClick!(swatches[2]);
        Assert.Equal(1, dialog.ColorIndex);

        swatches[0].OnClick!(swatches[0]);
        Assert.Null(dialog.ColorIndex);
    }

    [Fact]
    public void Reset_PutsBackAFreshForm()
    {
        var dialog = NewDialog();
        dialog.Kind = TrackKind.Wave;
        dialog.ColorIndex = 0;
        dialog.NameInput.Value = "Old";

        dialog.Reset("Track 7", new Dictionary<string, ThirtyDollarConverter.Parser.Sound>());

        Assert.Equal(TrackKind.PianoRoll, dialog.Kind);
        Assert.Null(dialog.ColorIndex);
        Assert.Equal("Track 7", dialog.NameInput.Value);
    }
}
