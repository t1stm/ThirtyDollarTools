using System.Globalization;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace ThirtyDollarConverter.Editor;

public static class SequenceText
{
    /// <summary>
    ///     Serializes a sequence to .tdw text with full value precision.
    ///     BaseEvent.Stringify rounds values to 2 decimals, which would corrupt the
    ///     builder's fractional stops. The builder emits sounds (with volume, pan and
    ///     sound-start offset), !speed/!stop/!combine/!divider/!cut/!transpose/!volume, and a
    ///     faithful clip's own actions verbatim.
    ///     Laid out the way the site saves (generateSequenceFile in its 💾.js): one line,
    ///     broken only after a "!divider", so every section reads as a line.
    /// </summary>
    public static string Serialize(Sequence sequence)
    {
        return string.Join("|", sequence.Events.Select(e => e.SoundEvent == "!divider" ? "!divider\n" : Stringify(e)));
    }

    private static string Stringify(BaseEvent e)
    {
        // A bare SoundEvent ("!cut") would be a global cut; an individual cut must
        // stay per-sound ("!cut@a|!cut@b").
        if (e is IndividualCutEvent cut) return cut.Stringify();

        var text = e.SoundEvent!;
        // A transpose back to 0 says so: a bare "!transpose" leaves the value to the reader.
        if (e.Value != 0 || e.SoundEvent == "!transpose") text += $"@{Exact(e.Value)}";
        text += e.ValueScale switch
        {
            ValueScale.Times => "@x",
            ValueScale.Divide => "@/",
            ValueScale.Add => "@+",
            _ => ""
        };
        if (e.Volume is { } volume) text += $"%{Exact(volume)}";
        if (e is not ExtendedEvent extended) return text;

        // Same suffix order as ExtendedEvent.Stringify: ^pan then >offset.
        if (extended.Pan != 0) text += $"^{Exact(extended.Pan)}";
        if (extended.OffsetInSeconds != 0) text += $">{Exact(extended.OffsetInSeconds)}";
        return text;
    }

    private static string Exact(double value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}