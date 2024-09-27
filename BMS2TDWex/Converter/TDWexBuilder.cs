using System.Text;

namespace BMS2TDW.Converter;

public class TDWexBuilder
{
    private readonly StringBuilder Builder = new();

    private void WriteDefine(string define_name)
    {
        var define = $"""
                      #define({define_name})|
                      _pause|
                      #enddefine|


                      """;

        Builder.Append(define);
    }

    public void ConvertBMSLevel(BMSLevel bms_level)
    {
        var added_hash_set = new HashSet<string>();
        foreach (var (_, sound) in bms_level.Header.ChannelMap)
        {
            if (added_hash_set.Contains(sound)) continue;

            WriteDefine(sound);
            added_hash_set.Add(sound);
        }

        Builder.Append("# Starting cover definitions. |\n");

        var header = bms_level.Header;
        Builder.Append($"!speed@{header.BPM}|");
        Builder.Append("!divider|\n\n");

        var source_division = 1;

        foreach (var (_, bms_measure) in bms_level.Data.Measures)
        {
            var max_measure_division = bms_measure.Events.Max(r => r.bms_event.BeatsDivision);
            if (max_measure_division != source_division)
            {
                Builder.Append($"!speed@{(float)max_measure_division / source_division}@x|");
                source_division = max_measure_division;
            }

            var tdw_measure = new TDWMeasure(max_measure_division);

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

            Builder.Append(tdw_measure.Export());
            Builder.Append("!divider|\n\n");
        }
    }

    public string Export()
    {
        return Builder.ToString();
    }
}