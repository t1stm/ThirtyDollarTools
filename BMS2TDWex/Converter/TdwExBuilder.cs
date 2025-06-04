using System.Text;

namespace BMS2TDW.Converter;

public class TdwExBuilder
{
    private readonly StringBuilder _builder = new();

    private void WriteDefine(string defineName)
    {
        var define = $"""
                      #define({defineName})|
                      _pause|
                      #enddefine|


                      """;

        _builder.Append(define);
    }

    public void ConvertBmsLevel(BmsLevel bmsLevel)
    {
        var added_hash_set = new HashSet<string>();
        foreach (var (_, sound) in bmsLevel.Header.ChannelMap)
        {
            if (added_hash_set.Contains(sound)) continue;

            WriteDefine(sound);
            added_hash_set.Add(sound);
        }

        _builder.Append("# Starting cover definitions. |\n");

        var header = bmsLevel.Header;
        _builder.Append($"!speed@{header.Bpm}|");
        _builder.Append("!divider|\n\n");

        var source_division = 1;

        foreach (var (_, bms_measure) in bmsLevel.Data.Measures)
        {
            var max_measure_division = bms_measure.Events.Max(r => r.bms_event.BeatsDivision);
            if (max_measure_division != source_division)
            {
                _builder.Append($"!speed@{(float)max_measure_division / source_division}@x|");
                source_division = max_measure_division;
            }

            var tdw_measure = new TdwMeasure(max_measure_division);

            foreach (var (_, bms_event) in bms_measure.Events)
            {
                var division = bms_event.BeatsDivision;

                var sounds = bms_event.SoundsArray;
                if (sounds is null) continue;

                for (var i = 0; i < sounds.Length; i++)
                {
                    var ev = sounds[i];
                    tdw_measure.PlaceEvent(division, i, ev);
                }
            }

            _builder.Append(tdw_measure.Export());
            _builder.Append("!divider|\n\n");
        }
    }

    public string Export()
    {
        return _builder.ToString();
    }
}