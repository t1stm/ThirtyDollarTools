using System.Globalization;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;

namespace ThirtyDollarConverter.Editor;

public static class SequenceText
{
    /// <summary>
    ///     Serializes a sequence to .tdw text with full value precision.
    ///     BaseEvent.Stringify rounds values to 2 decimals, which would corrupt the
    ///     builder's fractional stops. The builder emits sounds (with volume, pan and
    ///     sound-start offset), !speed/!stop/!combine/!divider — nothing else.
    /// </summary>
    public static string Serialize(Sequence sequence)
    {
        return string.Join("|\n", sequence.Events.Select(Stringify));
    }

    private static string Stringify(BaseEvent e)
    {
        var text = e.SoundEvent!;
        if (e.Value != 0) text += $"@{Exact(e.Value)}";
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
