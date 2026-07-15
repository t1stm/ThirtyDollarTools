namespace ThirtyDollarConverter.Editor;

/// <summary>
///     Cosmetic options for the exported sequence text. Nothing here affects timing:
///     "!divider" takes no time in TDW, it only breaks the event wall into readable
///     sections.
/// </summary>
public class SequenceStyle
{
    /// <summary>
    ///     Insert "!divider" every this many bars. Bars are counted across segments;
    ///     the merged project export follows the first track's bar structure. The count
    ///     restarts at every emitted "!speed" change, so each tempo section gets its own
    ///     bar numbering. Null (or &lt; 1) = off.
    /// </summary>
    public int? DividerEveryBars { get; set; }

    /// <summary>
    ///     Insert "!divider" before every mid-sequence "!speed" change.
    /// </summary>
    public bool DividerOnSpeedChanges { get; set; }

    /// <summary>
    ///     Whole-step gaps at or above this many steps render as "!stop@n"; shorter
    ///     ones as repeated "_pause" (one step each — "_pause" == "!stop@1").
    ///     Null = never use "!stop": every whole gap becomes "_pause"s.
    ///     Fractional gaps always render as "!stop" — exact timing needs them.
    ///     The default of 8 means "!stop" after 8 beats of silence.
    /// </summary>
    public int? MigrateToStop { get; set; } = 8;
}
