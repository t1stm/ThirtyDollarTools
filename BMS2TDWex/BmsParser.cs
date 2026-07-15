using System.Globalization;

namespace BMS2TDW;

public static class BmsParser
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static BmsChart Parse(string text)
    {
        var chart = new BmsChart();

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith('#')) continue;
            var body = line[1..];

            // #mmmcc:data object line
            if (body.Length > 6 && body[5] == ':' && char.IsDigit(body[0]))
            {
                ParseObjectLine(chart, body);
                continue;
            }

            var space = body.IndexOf(' ');
            if (space < 0) continue;
            var name = body[..space].ToUpperInvariant();
            var value = body[(space + 1)..].Trim();

            if (name.Length == 5 && name.StartsWith("WAV"))
                chart.Sounds[Base36(name[3..])] = Path.GetFileNameWithoutExtension(value);
            else if (name.Length == 5 && name.StartsWith("BPM"))
                chart.ExBpms[Base36(name[3..])] = double.Parse(value, Inv);
            else if (name.Length == 6 && name.StartsWith("STOP"))
                chart.Stops[Base36(name[4..])] = double.Parse(value, Inv);
            else
                switch (name)
                {
                    case "BPM":
                        chart.Bpm = double.Parse(value, Inv);
                        break;
                    case "TITLE":
                        chart.Title = value;
                        break;
                    case "ARTIST":
                        chart.Artist = value;
                        break;
                    case "GENRE":
                        chart.Genre = value;
                        break;
                }
        }

        return chart;
    }

    private static void ParseObjectLine(BmsChart chart, string body)
    {
        if (!int.TryParse(body[..3], NumberStyles.None, Inv, out var measure)) return;
        if (!int.TryParse(body[3..5], NumberStyles.None, Inv, out var channel)) return;
        var data = body[6..].Trim();
        if (data.Length == 0) return;

        // Channel 02 is a plain decimal measure-length multiplier, not object pairs.
        if (channel == 2)
        {
            chart.Meters[measure] = decimal.Parse(data, Inv);
            return;
        }

        var objects = new int[data.Length / 2];
        for (var i = 0; i < objects.Length; i++)
        {
            var pair = data.Substring(i * 2, 2);
            // Channel 03 encodes the BPM directly as hex; everything else is base-36 indices.
            objects[i] = channel == 3 ? Convert.ToInt32(pair, 16) : Base36(pair);
        }

        if (!chart.Measures.TryGetValue(measure, out var lines))
            chart.Measures[measure] = lines = [];
        lines.Add(new BmsLine(channel, objects));
    }

    private static int Base36(string s)
    {
        var value = 0;
        foreach (var upper in s.Select(char.ToUpperInvariant))
            value = value * 36 + upper switch
            {
                >= '0' and <= '9' => upper - '0',
                >= 'A' and <= 'Z' => upper - 'A' + 10,
                _ => 0
            };

        return value;
    }
}
