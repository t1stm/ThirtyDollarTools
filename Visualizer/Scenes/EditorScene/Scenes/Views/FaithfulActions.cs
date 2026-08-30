using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Scenes.Views;

/// <summary>
///     The span a scroll gesture keeps an action's value inside, and how far one notch moves
///     it - thirtydollar.website's own <c>set</c> bounds (its <c>actions</c> table).
/// </summary>
public readonly record struct ScrollRange(double Min, double Max, double Step = 1);

/// <summary>
///     One entry of the palette's Actions section, in the website's order. Every one is in the
///     atlas (the downloader maps <c>action_*</c> to <c>!*</c>), so the palette draws them with
///     the same <c>RenderableFactory</c> the sounds use.
///     <c>_pause</c> is the one entry that isn't an action - TDW's silent sound, the only way to
///     leave a gap - and it rides the same item shape: the walker advances the position for it
///     and emits nothing.
///     <paramref name="Template" /> is the TDW text a fresh item starts from - the site's own
///     default, or null for the actions that take no amount. It is offered for editing rather
///     than applied blind, which is also how "!speed@2@x" and the two-value "!pulse"/"!bg"
///     payloads get built without a form per action.
///     <paramref name="Scroll" /> is what the scroll gesture may do to the slot once it is in
///     the sequence, or null for the slots that take nothing from the wheel: the valueless
///     actions, "_pause", and the two that pack two values into one ("!bg"'s color and fade,
///     "!pulse"'s repeats and frequency), which are only editable through the right-click
///     dialog.
/// </summary>
public sealed record FaithfulAction(string Name, string? Template, string Hint, ScrollRange? Scroll = null)
{
    public static readonly IReadOnlyList<FaithfulAction> All =
    [
        new("_pause", null, "A silent step - the only way to leave a gap"),
        new("!speed", "!speed@300", "Sets the tempo, in events per minute", new ScrollRange(10, 10000)),
        new("!volume", "!volume@100", "Sets the sequence volume, in percent", new ScrollRange(0, 600)),
        new("!stop", "!stop@4", "Waits the given number of steps", new ScrollRange(0, 1000)),
        new("!divider", null, "Breaks the line - purely visual"),
        new("!combine", null, "Plays the next sound on the same step as this one"),
        new("!transpose", "!transpose@1", "Shifts every following sound, in semitones", new ScrollRange(-60, 60)),
        new("!loop", null, "Jumps back to !looptarget once"),
        new("!loopmany", "!loopmany@4", "Jumps back to !looptarget the given number of times",
            new ScrollRange(1, 1000)),
        new("!looptarget", null, "Where !loop and !loopmany jump back to"),
        new("!jump", "!jump@1", "Jumps to the !target with this number, once", new ScrollRange(1, 9999)),
        new("!target", "!target@1", "A numbered landing point for !jump", new ScrollRange(1, 9999)),
        new("!cut", null, "Silences every sound this track plays"),
        new("!flash", null, "Flashes the screen"),
        new("!bg", "!bg@#ff0000,0.5", "Fades the background to a color over N seconds"),
        new("!pulse", "!pulse@4,100", "Pulses the background: repeats, then frequency")
    ];

    /// <summary>The palette entry for an event name, or null - "!divider", "_pause" and friends.</summary>
    public static FaithfulAction? Find(string? name)
    {
        return All.FirstOrDefault(action => action.Name == name);
    }

    /// <summary>
    ///     What one notch of the scroll gesture may do to this event, or null when the slot
    ///     takes nothing from the wheel at all - in which case the scroll belongs to the view.
    ///     The bounds follow the event's own scale, as the site's do: a scaled "!speed@2@x" is
    ///     a factor rather than a tempo and moves in tenths, and an "@+" one is the same span
    ///     opened up to negatives ("!speed@-100@+" is a slowdown).
    /// </summary>
    public static ScrollRange? ScrollRangeFor(BaseEvent ev)
    {
        if (Find(ev.SoundEvent)?.Scroll is not { } range) return null;

        return ev.ValueScale switch
        {
            ValueScale.Times => new ScrollRange(0.01, 1000, 0.1),
            ValueScale.Divide => new ScrollRange(0.1, 100, 0.1),
            ValueScale.Add => range with { Min = -range.Max },
            _ => range
        };
    }

    /// <summary>
    ///     Whether this event carries a value at all - the slots the site reopens its form for
    ///     on a right click, which is the only way to reach a packed "!bg"/"!pulse" payload.
    /// </summary>
    public static bool TakesValue(string? name)
    {
        return Find(name)?.Template is not null;
    }

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
