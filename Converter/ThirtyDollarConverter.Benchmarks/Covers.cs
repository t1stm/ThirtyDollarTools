using ThirtyDollarParser;

namespace ThirtyDollarConverter.Benchmarks;

/// <summary>
///     Real covers out of ~/tdw, spanning a size range, with a "retune one note in the middle"
///     edit each - the cheapest edit an editor can make, and the one incremental rendering
///     exists for. <see cref="IcutHeavy" /> is the interesting one: its cuts are what makes the
///     subtract-based renderer give up and re-render everything.
/// </summary>
public sealed record Cover(string Name, string Path)
{
    public const string IcutHeavy = "dk64-icut";

    public override string ToString()
    {
        return Name;
    }
}

public static class Covers
{
    public static readonly Cover[] All =
    [
        new("veil", "veil.🗿"),
        new(Cover.IcutHeavy, "dk64-bonus-barrel-icut.ver.🗿"),
        new("bad-apple", "other/(Domburg) bad apple full.🗿"),
        new("another-medium", "another medium/another_medium.🗿"),
        new("sunset-jesus", "other/(cyvos) Avicii - Sunset Jesus.🗿"),
        new("gt3-seq6", "GT3 MIDI Conversions_arcade_music_Seq6.🗿")
    ];

    /// <summary>
    ///     Retunes one sound event in the middle of the sequence, cycling through a few values so
    ///     every invocation is an edit of the same size against the previous state. Returns false
    ///     when the sequence has no sound event to retune.
    /// </summary>
    public static bool RetuneMiddleEvent(Sequence sequence, int step)
    {
        var sounds = new List<BaseEvent>();
        foreach (var ev in sequence.Events)
            if (ev.SoundEvent is { } name && !name.StartsWith('!') && name != "_pause")
                sounds.Add(ev);

        if (sounds.Count == 0) return false;

        var target = sounds[sounds.Count / 2];
        var value = step % 5 - 2; // -2..2, never sitting on the same value twice in a row
        target.Value = value;
        target.WorkingValue = value;
        return true;
    }
}