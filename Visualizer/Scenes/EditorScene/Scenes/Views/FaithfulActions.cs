using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Scenes.Views;

/// <summary>
///     What the palette's Actions section offers, in the website's order. Every one of them is
///     already in the atlas (the downloader maps <c>action_*</c> to <c>!*</c>), so the palette
///     draws them with the same <c>RenderableFactory</c> the sounds use.
///     <c>_pause</c> is the one entry that isn't an action - it is TDW's silent sound, the only
///     way to leave a gap, and it belongs beside the actions rather than in a palette of
///     project instruments. It rides the same item shape: the walker advances the position for
///     it and emits nothing, exactly as it does inside an imported sequence.
///     <paramref name="Template" /> is the TDW text a fresh item starts from - null for the
///     actions that take no amount, and the site's own default otherwise. It is offered for
///     editing rather than applied blind, which is also how "!speed@2@x" and the two-value
///     "!pulse"/"!bg" payloads get built without a form per action.
/// </summary>
public sealed record FaithfulAction(string Name, string? Template, string Hint)
{
    public static readonly IReadOnlyList<FaithfulAction> All =
    [
        new("_pause", null, "A silent step - the only way to leave a gap"),
        new("!speed", "!speed@300", "Sets the tempo, in events per minute"),
        new("!volume", "!volume@100", "Sets the sequence volume, in percent"),
        new("!stop", "!stop@4", "Waits the given number of steps"),
        new("!divider", null, "Breaks the line - purely visual"),
        new("!combine", null, "Plays the next sound on the same step as this one"),
        new("!transpose", "!transpose@1", "Shifts every following sound, in semitones"),
        new("!loop", null, "Jumps back to !looptarget once"),
        new("!loopmany", "!loopmany@4", "Jumps back to !looptarget the given number of times"),
        new("!looptarget", null, "Where !loop and !loopmany jump back to"),
        new("!jump", "!jump@1", "Jumps to the !target with this number, once"),
        new("!target", "!target@1", "A numbered landing point for !jump"),
        new("!cut", null, "Silences every sound this track plays"),
        new("!flash", null, "Flashes the screen"),
        new("!bg", "!bg@#ff0000,0.5", "Fades the background to a color over N seconds"),
        new("!pulse", "!pulse@4,100", "Pulses the background: repeats, then frequency")
    ];

    /// <summary>
    ///     What the palette draws for this action: the template's event, so an entry with an
    ///     amount shows the default it will be inserted with ("!speed" reads 300) instead of
    ///     the bare zero a template-less event carries.
    /// </summary>
    public BaseEvent PaletteEvent()
    {
        return (Template is not null ? FaithfulItem.Parse(Template)?.Action : null)
               ?? new NormalEvent { SoundEvent = Name, ValueScale = ValueScale.None };
    }
}
