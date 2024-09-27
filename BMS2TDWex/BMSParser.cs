using BMS2TDW.Objects;

namespace BMS2TDW;

public static class BMSParser
{
    public static BMSLevel ParseFile(string read)
    {
        var split = read.Split('\n');
        var level = new BMSLevel();
        bool? is_header_currently = null;

        foreach (var current_line in split)
        {
            var line = current_line.Trim();
            is_header_currently = line switch
            {
                "*---------------------- HEADER FIELD" => true,
                "*---------------------- MAIN DATA FIELD" => false,
                _ => is_header_currently
            };

            if (is_header_currently is null) continue;

            switch (is_header_currently)
            {
                case true:
                    HandleHeaderEvents(ref level.Header, line);
                    continue;

                case false:
                    HandleMainDataEvents(ref level.Data, in level.Header, line);
                    continue;
            }
        }

        return level;
    }

    private static void HandleHeaderEvents(ref BMSHeader header, string line)
    {
        if (!line.StartsWith('#')) return;

        var ev = line[1..];
        var event_split = ev.Split(' ');

        var event_name = event_split[0];
        var remaining_event = ev[event_name.Length..].Trim();

        if (event_name.StartsWith("WAV"))
        {
            var wave_number = event_name[3..];
            // remaining_event is the wave file defined to this index.
            header.ChannelMap.Add(string.Intern(wave_number), string.Intern(remaining_event[..^4]));
            return;
        }

        switch (event_name)
        {
            case "PLAYER":
                if (int.TryParse(remaining_event, out var player_count))
                    header.PlayerCount = player_count;
                break;

            case "GENRE":
                header.Genre = remaining_event;
                break;

            case "TITLE":
                header.Title = remaining_event;
                break;

            case "ARTIST":
                header.Artist = remaining_event;
                break;

            case "BPM":
                if (int.TryParse(remaining_event, out var bpm))
                    header.BPM = bpm;
                break;

            case "PLAYLEVEL":
                if (int.TryParse(remaining_event, out var play_level))
                    header.PlayLevel = play_level;
                break;

            case "RANK":
                if (int.TryParse(remaining_event, out var rank))
                    header.Rank = rank;
                break;

            case "TOTAL":
                if (int.TryParse(remaining_event, out var total))
                    header.Total = total;
                break;

            case "STAGEFILE":
                header.StageFile = true;
                break;
        }
    }

    private static void HandleMainDataEvents(ref BMSData data, in BMSHeader level_header, string line)
    {
        if (!line.StartsWith('#')) return;
        var clean_value = line[1..];

        var split = clean_value.Split(':');
        if (split.Length != 2) return;

        var measure_channel = split[0];
        var event_data = split[1];
        var measure_string = measure_channel[..3];
        var channel_string = measure_channel[3..];

        var measure_number = int.Parse(measure_string);
        var channel_number = int.Parse(channel_string);

        if (!data.Measures.TryGetValue(measure_number, out var value))
        {
            value = new BMSMeasure();
            data.Measures[measure_number] = value;
        }

        var measure = value;
        var beats_division = event_data.Length / 2;

        var new_event = new BMSEvent
        {
            BeatsDivision = beats_division,
            StringValue = event_data
        };

        if (channel_number is 1 or >= 11 and <= 27)
        {
            var array = new_event.SoundsArray = new string[beats_division];
            for (var i = 0; i < array.Length; i++)
            {
                var index = string.Concat(event_data[i * 2], event_data[i * 2 + 1]);
                array[i] = string.Intern(level_header.ChannelMap[index]);
            }
        }

        measure.Events.Add((channel_number, new_event));
    }
}