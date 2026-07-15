namespace BMS2TDW;

/// <summary>
///     A parsed BMS chart: header metadata, definition tables and the raw object lines
///     grouped by measure. Reference: https://hitkey.nekokan.dyndns.info/cmds.htm
/// </summary>
public class BmsChart
{
    /// <summary>#BPMxx: base-36 index to an extended BPM value (channel 08 references these).</summary>
    public readonly Dictionary<int, double> ExBpms = new();

    public readonly Dictionary<int, List<BmsLine>> Measures = new();

    /// <summary>Channel 02: measure length multiplier (1 = a 4/4 bar).</summary>
    public readonly Dictionary<int, decimal> Meters = new();

    /// <summary>#WAVxx: base-36 index to sample name (extension stripped).</summary>
    public readonly Dictionary<int, string> Sounds = new();

    /// <summary>#STOPxx: base-36 index to a pause length in 1/192ths of a whole note.</summary>
    public readonly Dictionary<int, double> Stops = new();

    public string Artist = "";
    public double Bpm = 130;
    public string Genre = "";
    public string Title = "";
}

/// <summary>
///     One object line: its channel and the evenly-spaced object values across the
///     measure. 0 means no object at that position.
/// </summary>
public record BmsLine(int Channel, int[] Objects);