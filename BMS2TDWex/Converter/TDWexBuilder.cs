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
        var skip_generation = true;
        
        foreach (var (_, sound) in bms_level.Header.ChannelMap)
        {
            if (added_hash_set.Contains(sound) || skip_generation) continue;

            WriteDefine(sound);
            added_hash_set.Add(sound);
        }

        Builder.Append("# Starting cover definitions. |\n");

        var header = bms_level.Header;
        var starting_bpm = header.BPM;

        var working_bpm = (double)starting_bpm;
        var last_bpm = working_bpm;

        Builder.Append($"!speed@{working_bpm}|");
        Builder.Append("!divider|\n\n");
        
        var source_division = 1;

        foreach (var (measure_number, bms_measure) in bms_level.Data.Measures.OrderBy(kvp => kvp.Key))
        {
            if (bms_measure.BPM != null)
            {
                working_bpm = bms_measure.BPM.Value;
            }

            var updated_bpm = Math.Abs(working_bpm - last_bpm) > 0.01;

            var max_measure_division = bms_measure.Events.Max(r => r.bms_event.BeatsDivision);
            if (updated_bpm)
            {
                Builder.Append($"!speed@{working_bpm}|");
            }
            
            if (max_measure_division != source_division)
            {
                Builder.Append($"!speed@{(float)max_measure_division / source_division}@x|");
                source_division = max_measure_division;
            }
            
            var tdw_measure = new TDWMeasure(max_measure_division);

            foreach (var (bms_channel, bms_event) in bms_measure.Events)
            {
                var division = bms_event.BeatsDivision;
                var sounds = bms_channel switch
                {
                    8 => bms_event.BPMArray,
                    9 => bms_event.StopArray,
                    _ => bms_event.SoundsArray
                };
                
                if (sounds is null) continue;
                
                for (var i = 0; i < sounds.Length; i++)
                {
                    var event_value = sounds[i];
                    if (bms_channel is 8 or 9 && event_value == "") 
                        continue;
                    
                    string? event_name;
                    switch (bms_channel)
                    {
                        case 8:
                            var value = double.Parse(event_value); 
                            event_name = $"!speed@{value}|!speed@{max_measure_division}@x|!speed@{bms_measure.Length}@/";
                            working_bpm = value;
                            break;
                        case 9:
                            event_name = $"!stop@{double.Parse(event_value) / 192}|";
                            break;
                        default:
                            event_name = event_value;
                            break;
                    }

                    tdw_measure.PlaceEvent(division, i, event_name);
                }
            }

            last_bpm = working_bpm;
            Builder.Append(tdw_measure.Export());
            Builder.Append($"!divider@{measure_number}|\n\n"); 
        }
    }

    public string Export()
    {
        return Builder.ToString();
    }
}