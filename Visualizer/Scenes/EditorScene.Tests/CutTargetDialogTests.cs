using EditorScene.Scenes.Dialogs;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace EditorScene.Tests;

public class CutTargetDialogTests
{
    private static readonly Instrument Kit = Layered("kit", "kick", "hat");
    private static readonly Instrument Bell = Layered("bell", "bell");
    private static readonly Instrument Empty = new() { Name = "empty" };

    private static Instrument Layered(string name, params string[] sounds)
    {
        var instrument = new Instrument { Name = name };
        foreach (var sound in sounds) instrument.AddSound(sound);
        return instrument;
    }

    private static CutTargetDialog Open(FaithfulItem cut)
    {
        var context = new EditorTestContext();
        return new CutTargetDialog(context, context.NewAtlasStore(), [Empty, Kit, Bell], ["kick", "hat", "bell"], cut);
    }

    private static KeyboardKeyEventArgs Key(Keys key) => new(key, 0, 0, false);

    private static IEnumerable<UIElement> Walk(UIElement element)
    {
        yield return element;
        if (element is not Panel panel) yield break;
        foreach (var child in panel.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    [Fact]
    public void StartsOnWhatTheCutSilencesNow()
    {
        var instrumentCut = Open(FaithfulItem.Cut(Kit));
        Assert.Equal(CutTarget.Instrument, instrumentCut.Target);
        Assert.Same(Kit, instrumentCut.Instrument);

        var soundCut = Open(FaithfulItem.Cut("bell"));
        Assert.Equal(CutTarget.Sound, soundCut.Target);
        Assert.Equal("bell", soundCut.Sound);
        Assert.Same(Bell, soundCut.Instrument);

        Assert.Equal(CutTarget.Every, Open(FaithfulItem.Parse("!cut")!).Target);
    }

    /// <summary>A converted piano roll leaves its cut notes as sound lists; one that matches an instrument links back to it.</summary>
    [Fact]
    public void ASoundListMatchingAnInstrument_StartsOnThatInstrument()
    {
        var dialog = Open(new FaithfulItem { Action = new IndividualCutEvent(["hat", "kick"]) });

        Assert.Equal(CutTarget.Instrument, dialog.Target);
        Assert.Same(Kit, dialog.Instrument);
    }

    [Fact]
    public void PickingASound_TargetsThatSoundAlone()
    {
        var dialog = Open(FaithfulItem.Cut(Kit));

        dialog.Pick("hat");

        var cut = Assert.IsType<IndividualCutEvent>(dialog.Result.Action);
        Assert.Equal(["hat"], cut.CutSounds);
    }

    [Fact]
    public void PickingAnInstrument_TargetsAllOfIt_OrItsFirstSoundUnderOneSound()
    {
        var dialog = Open(FaithfulItem.Parse("!cut")!);

        dialog.Pick(Kit);
        Assert.Equal(CutTarget.Instrument, dialog.Target);
        Assert.True(dialog.Result.Note is { IsCut: true } note && note.Instrument == Kit);

        dialog.Pick("hat");
        dialog.Pick(Bell);
        Assert.Equal(CutTarget.Sound, dialog.Target);
        Assert.Equal("bell", dialog.Sound);
    }

    [Fact]
    public void Keys_PickTargetsInstrumentsAndSounds()
    {
        var dialog = Open(FaithfulItem.Cut(Kit));

        Assert.True(dialog.HandleKey(Key(Keys.D1)));
        Assert.Equal(CutTarget.Every, dialog.Target);
        Assert.IsNotType<IndividualCutEvent>(dialog.Result.Action);

        dialog.HandleKey(Key(Keys.D2));
        dialog.HandleKey(Key(Keys.Right));
        Assert.Equal(CutTarget.Sound, dialog.Target);
        Assert.Equal("kick", dialog.Sound);
        dialog.HandleKey(Key(Keys.Right));
        Assert.Equal("hat", dialog.Sound);

        dialog.HandleKey(Key(Keys.Down));
        Assert.Same(Bell, dialog.Instrument);
        Assert.Equal("bell", dialog.Sound);
    }

    /// <summary>An instrument with no sounds has nothing to cut, so it is not offered.</summary>
    [Fact]
    public void InstrumentsWithoutSounds_AreLeftOut()
    {
        var dialog = Open(FaithfulItem.Cut(Kit));

        var names = Walk(dialog.Element).OfType<Sundex.Components.Labels.Label>()
            .Select(label => label.Value.ToString().TrimEnd('\0')).ToArray();
        Assert.Contains("kit", names);
        Assert.DoesNotContain("empty", names);
    }

    [Fact]
    public void ClickingATargetRow_PicksIt()
    {
        var dialog = Open(FaithfulItem.Cut(Kit));
        var row = Walk(dialog.Element).Single(element => element.ID == "cut-one-sound");

        row.OnClick!(row);

        Assert.Equal(CutTarget.Sound, dialog.Target);
        Assert.Equal("kick", dialog.Sound);
        Assert.Contains("track-kind-row-selected", row.Classes);
    }
}
