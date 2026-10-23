using EditorScene.Scenes.Dialogs;
using EditorScene.Scenes.Views;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Inputs;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Tests;

public class ActionValueDialogTests
{
    private static FaithfulTrack Track(params string[] tdw)
    {
        var track = new FaithfulTrack(new TimingInfo(), 1);
        foreach (var text in tdw) track.Items.Add(FaithfulItem.Parse(text)!);
        return track;
    }

    /// <summary>A new slot from the palette, added at the end.</summary>
    private static ActionValueDialog Add(string action, FaithfulTrack track)
    {
        return new ActionValueDialog(new EditorTestContext(), FaithfulAction.Find(action)!, track, track.Items.Count);
    }

    /// <summary>The right-click form on the track's item at <paramref name="index" />.</summary>
    private static ActionValueDialog Edit(FaithfulTrack track, int index)
    {
        var current = track.Items[index].Action!;
        return new ActionValueDialog(new EditorTestContext(), FaithfulAction.Find(current.SoundEvent)!, track, index,
            current);
    }

    private static KeyboardKeyEventArgs Key(Keys key) => new(key, 0, 0, false);

    private static string[] Texts(ActionValueDialog dialog) => [.. dialog.Facts.Select(fact => fact.Text)];

    [Theory]
    [InlineData(5, 4, 0, 5)] // fewer slots than fit: all of them, from the left
    [InlineData(30, 15, 11, 9)] // room on both sides: centred
    [InlineData(30, 2, 0, 9)] // near the start: from the start
    [InlineData(21, 20, 12, 9)] // a new slot at the end: the last nine
    public void Window_CentresOnlyWhenBothSidesHaveRoom(int total, int slot, int start, int count)
    {
        Assert.Equal((start, count), ActionValueDialog.Window(total, slot, ActionValueDialog.StripSlots));
    }

    [Fact]
    public void ANewSlot_SitsAtTheStripsRightEnd_AnEditedOne_InItsMiddle()
    {
        var track = Track([.. Enumerable.Repeat("!stop@1", 20)]);
        Assert.Equal((12, 8), Add("!speed", track).StripWindow);
        Assert.Equal((6, 4), Edit(track, 10).StripWindow);
    }

    [Fact]
    public void ANewSlot_StartsFromThePalettesDefault()
    {
        var dialog = Add("!speed", Track());
        Assert.Equal(ValueScale.None, dialog.Mode);
        Assert.Equal("!speed@300", dialog.Tdw);
        Assert.Equal("!speed", dialog.Result!.Action!.SoundEvent);
    }

    /// <summary>S/A/M/D pick how the value applies, from the focused value field and from the dialog alike.</summary>
    [Fact]
    public void ModeKeys_PickHowTheValueApplies()
    {
        var dialog = Add("!speed", Track());
        Assert.True(dialog.MainField.HandleKeyDown(Key(Keys.M)));
        Assert.Equal(ValueScale.Times, dialog.Mode);
        Assert.True(dialog.HandleKey(Key(Keys.D)));
        Assert.Equal(ValueScale.Divide, dialog.Mode);

        // Transpose only sets or adds; Multiply is not a key it takes.
        var transpose = Add("!transpose", Track());
        Assert.False(transpose.HandleKey(Key(Keys.M)));
        Assert.True(transpose.HandleKey(Key(Keys.A)));
        Assert.Equal("!transpose@1@+", transpose.Tdw);
    }

    [Fact]
    public void AMode_KeepsTheValueInsideItsOwnBounds()
    {
        var dialog = Add("!speed", Track());
        dialog.Pick(ValueScale.Times); // Multiply goes from 0.01 to 1000
        ((NumericInput)dialog.MainField).Value = 5000;
        Assert.Equal("!speed@1000@x", dialog.Tdw);
    }

    [Theory]
    [InlineData("!volume@0.5@x")]
    [InlineData("!transpose@-12@+")]
    [InlineData("!bg@#E0AF68,1")]
    [InlineData("!bg@#E0AF6880,0.25")]
    [InlineData("!pulse@4,2")]
    public void AnEdit_StartsOnWhatTheSlotSaysNow(string tdw)
    {
        var dialog = Edit(Track(tdw), 0);
        Assert.Equal(tdw, dialog.Tdw);
    }

    [Fact]
    public void Facts_ReadTheTrackBeforeTheSlot()
    {
        var dialog = Add("!speed", Track("!speed@200", "!stop@2"));
        dialog.Pick(ValueScale.Times);
        ((NumericInput)dialog.MainField).Value = 2;
        Assert.Equal(["Tempo before: 200 BPM", "After this: 400 BPM"], Texts(dialog));

        Assert.Equal(["Lasts 1.20 s at 200 BPM"], Texts(Add("!stop", Track("!speed@200"))));
    }

    [Fact]
    public void ABackground_FadesFromThePreviousOne_OrTheSitesWithNone()
    {
        Assert.Equal("Fades from #4C6BCC over 0.5 s", Add("!bg", Track("!bg@#4C6BCC,1")).Facts.Last().Text);
        Assert.Equal("Fades from #36393C over 0.5 s", Add("!bg", Track()).Facts.Last().Text);
    }

    /// <summary>The site numbers targets for you: a new one takes the first free number, a jump the first unused target.</summary>
    [Fact]
    public void TargetsAndJumps_StartOnTheNextFreeNumber()
    {
        var track = Track("!target@1", "!jump@1", "!target@2");
        Assert.Equal("!target@3", Add("!target", track).Tdw);
        Assert.Equal("!jump@2", Add("!jump", track).Tdw);
    }

    [Fact]
    public void ATargetOrJump_WarnsAboutWhatIsMissingOrTaken()
    {
        var track = Track("!target@1", "!stop@1");

        var jump = Add("!jump", track);
        ((NumericInput)jump.MainField).Value = 4;
        Assert.Contains(("No target 4 in this track yet", true), jump.Facts);

        var target = Add("!target", track);
        ((NumericInput)target.MainField).Value = 1;
        Assert.Contains(("Target 1 is already in this track, 2 slots back", true), target.Facts);

        // Editing the target itself is not a clash with itself.
        Assert.DoesNotContain(Edit(track, 0).Facts, fact => fact.Warn);
    }

    [Fact]
    public void AShortColor_WritesNothing()
    {
        var dialog = Add("!bg", Track());
        dialog.MainField.Value = "12";
        Assert.Null(dialog.Result);
        Assert.Contains(dialog.Facts, fact => fact.Warn);
    }
}
