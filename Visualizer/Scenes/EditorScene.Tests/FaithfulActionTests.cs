using EditorScene.Scenes.Views;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Tests;

/// <summary>
///     <see cref="FaithfulAction" />'s value table, against thirtydollar.website's own
///     <c>actions</c> list: which slots the scroll gesture may turn, how far one notch goes,
///     and which ones only their form can edit.
/// </summary>
public class FaithfulActionTests
{
    private static BaseEvent Event(string tdw)
    {
        return FaithfulItem.Parse(tdw)!.Action!;
    }

    /// <summary>
    ///     The site turns the wheel away from a slot with nothing to turn: the actions holding
    ///     no value, "_pause" (its sound id starts with "_"), and the two whose single value
    ///     packs two - "!bg"'s colour and fade, "!pulse"'s repeats and frequency.
    /// </summary>
    [Theory]
    [InlineData("_pause")]
    [InlineData("!divider")]
    [InlineData("!combine")]
    [InlineData("!loop")]
    [InlineData("!looptarget")]
    [InlineData("!flash")]
    [InlineData("!cut")]
    [InlineData("!bg@#ff0000,0.5")]
    [InlineData("!pulse@4,100")]
    public void ScrollRangeFor_IsNullOnSlotsWithNothingToTurn(string tdw)
    {
        Assert.Null(FaithfulAction.ScrollRangeFor(Event(tdw)));
    }

    [Theory]
    [InlineData("!speed@300", 10, 10000, 1)]
    [InlineData("!volume@100", 0, 600, 1)]
    [InlineData("!stop@4", 0, 1000, 1)]
    [InlineData("!transpose@1", -60, 60, 1)]
    [InlineData("!loopmany@4", 1, 1000, 1)]
    [InlineData("!jump@1", 1, 9999, 1)]
    [InlineData("!target@1", 1, 9999, 1)]
    public void ScrollRangeFor_MatchesTheSitesBounds(string tdw, double min, double max, double step)
    {
        var range = Assert.NotNull(FaithfulAction.ScrollRangeFor(Event(tdw)));
        Assert.Equal(new ScrollRange(min, max, step), range);
    }

    /// <summary>
    ///     A scaled action is a factor, not a tempo, so it moves in the site's tenths inside
    ///     its own bounds; an "@+" one is the same span opened up to negatives.
    /// </summary>
    [Fact]
    public void ScrollRangeFor_FollowsTheEventsScale()
    {
        Assert.Equal(new ScrollRange(0.01, 1000, 0.1), FaithfulAction.ScrollRangeFor(Event("!speed@2@x")));
        Assert.Equal(new ScrollRange(0.1, 100, 0.1), FaithfulAction.ScrollRangeFor(Event("!speed@2@/")));
        Assert.Equal(new ScrollRange(-10000, 10000), FaithfulAction.ScrollRangeFor(Event("!speed@100@+")));
        Assert.Equal(new ScrollRange(-600, 600), FaithfulAction.ScrollRangeFor(Event("!volume@50@+")));
    }

    /// <summary>
    ///     The right-click dialog is filled from the event's own text and commits by parsing it
    ///     back, so every action that opens one has to survive the round trip - the packed
    ///     "!bg" and "!pulse" above all, which is the whole reason the dialog exists.
    /// </summary>
    [Fact]
    public void TakesValue_ActionsRoundTripThroughTheirText()
    {
        foreach (var action in FaithfulAction.All.Where(a => FaithfulAction.TakesValue(a.Name)))
        {
            var original = Event(action.Template!);
            var reparsed = FaithfulItem.Parse(original.Stringify())?.Action;

            Assert.NotNull(reparsed);
            Assert.Equal(action.Name, reparsed.SoundEvent);
            Assert.Equal(original.Value, reparsed.Value, 3);
            Assert.Equal(original.ValueScale, reparsed.ValueScale);
        }
    }

    /// <summary>The valueless slots have no form to reopen, so a right click stays a preview.</summary>
    [Theory]
    [InlineData("_pause")]
    [InlineData("!divider")]
    [InlineData("!cut")]
    [InlineData("!flash")]
    public void TakesValue_IsFalseWithoutOne(string name)
    {
        Assert.False(FaithfulAction.TakesValue(name));
    }

    /// <summary>Text a user typed by hand reaches the parser, and the colour events throw on it.</summary>
    [Fact]
    public void Parse_ReturnsNullOnTextTheParserRejects()
    {
        Assert.Null(FaithfulItem.Parse("!bg@not-a-colour"));
        Assert.Null(FaithfulItem.Parse("!pulse@"));
        Assert.Null(FaithfulItem.Parse(""));
    }
}
