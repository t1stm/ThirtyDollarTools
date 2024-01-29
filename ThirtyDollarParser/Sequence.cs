using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using ThirtyDollarParser.Custom_Events;

namespace ThirtyDollarParser;

public class Sequence
{
    public Event[] Events { get; set; } = Array.Empty<Event>();
    public readonly Dictionary<string, Event[]> Definitions = new();
    public readonly HashSet<string> SeparatedChannels = new();
    
    private static readonly CultureInfo CultureInfo = CultureInfo.InvariantCulture;

    public Sequence Copy()
    {
        return new Sequence
        {
            Events = Events.Select(r => r.Copy()).ToArray()
        };
    }

    /// <summary>
    /// Parses a sequence stored in a string.
    /// </summary>
    /// <param name="data">The string containing the sequence.</param>
    /// <returns>The parsed sequence.</returns>
    public static Sequence FromString(string data)
    {
        var sequence = new Sequence();
        var split = data.Split('|');
        var list = new List<Event>();

        using var enumerator = split.AsEnumerable().GetEnumerator();
        while (enumerator.MoveNext())
        {
            var text = enumerator.Current;
            
            text = text.Replace("\n", "").Trim();
            if (string.IsNullOrEmpty(text)) continue;
            if (text.StartsWith('#'))
            {
                if (TryDefine(text, enumerator, sequence)) continue;
                if (TryIndividualCut(text, sequence, list)) continue;
            }

            if (text[1..].Any(ch => ch == '!'))
                // Text contains more than one parameter on event without divider. Adding the first event only.
                text = text[..text[1..].IndexOf('!')];
            
            var new_event = ParseEvent(text);
            
            if (new_event.SoundEvent?.StartsWith('!') ?? false)
            {
                list.Add(new_event);
                continue;
            }
            
            var repeats = new_event.PlayTimes;
            
            new_event.OriginalLoop = 1;
            new_event.PlayTimes = 1;
            for (var i = 0; i < repeats; i++)
            {
                if (ProcessDefines(sequence, new_event, list)) continue;
                list.Add(new_event.Copy());
            }
        }

        sequence.Events = list.ToArray();
        return sequence;
    }

    private static bool TryDefine(string text, IEnumerator<string> enumerator, Sequence comp)
    {
        var special_match = Regex.Match(text, @"^#(?<name>[^\s(]+)\((?<value>[^)]+)\)");
        if (!special_match.Success) return true;

        if (special_match.Groups["name"].Value != "define") return false;
        if (!enumerator.MoveNext()) return true;

        var defines = ParseDefines(in enumerator);
        var define_name = special_match.Groups["value"].Value;

        comp.Definitions.Add(define_name, defines);
        return true;
    }
    
    private static bool TryIndividualCut(string text, Sequence sequence, List<Event> event_list)
    {
        var match = Regex.Match(text, @"^#icut\((?<events>[^)]+)\)");
        if (!match.Success) return false;
        if (!match.Groups["events"].Success) return false;

        var cut_events = match.Groups["events"].Value;
        var split_events = cut_events.Split(',');
        
        for (var i = 0; i < split_events.Length; i++)
        {
            var name = split_events[i];
            split_events[i] = name.Trim();
        }

        var hash_set = new HashSet<string>(split_events);
        
        var new_event = new IndividualCutEvent(hash_set)
        {
            SoundEvent = text
        };
        event_list.Add(new_event);
        
        foreach (var ev in split_events)
        {
            sequence.SeparatedChannels.Add(ev);
        }
        
        return true;
    }

    private static Event[] ParseDefines(in IEnumerator<string> enumerator)
    {
        var events = new List<Event>();

        while (true)
        {
            var text = enumerator.Current;
            if (text.Trim() == "#enddefine") break;
            
            events.Add(ParseEvent(text));
            if (!enumerator.MoveNext()) break;
        }
        
        return events.ToArray();
    }

    private static bool ProcessDefines(Sequence comp, Event new_event, List<Event> list)
    {
        if (!comp.Definitions.TryGetValue(new_event.SoundEvent ?? "", out var events)) return false;
        var copy = events.Select(r => r.Copy()).ToArray();

        if (new_event is { Value: 0, ValueScale: ValueScale.None or ValueScale.Add })
        {
            goto return_path;
        }

        var val = new_event.Value;
        foreach (var ev in copy)
        {
            if (ev.SoundEvent is "!combine") continue;
            switch (new_event.ValueScale)
            {
                case ValueScale.None:
                case ValueScale.Add:
                    ev.Value += val;
                    break;

                case ValueScale.Divide:
                    ev.Value /= val;
                    break;

                case ValueScale.Times:
                    ev.Value *= val;
                    break;
            }
        }

        return_path:
        list.AddRange(copy);
        return true;
    }

    /// <summary>
    /// Parses a single event.
    /// </summary>
    /// <param name="text">The string of the event.</param>
    /// <returns>The parsed event.</returns>
    private static Event ParseEvent(string text)
    {
        if (text.StartsWith("!pulse") || text.StartsWith("!bg"))
        {
            // Special color lines get their own parser. 🗿
            return ParseColorEvent(text);
        }
        
        var split_for_value = text.Split('@');
        var split_for_repeats = text.Split('=');
        var value = 0.0;
        double? event_volume = null;
        var scale = ValueScale.None;
        var loop_times = 1;
        
        try
        {
            // Case !event@16
            if (split_for_value.Length > 1)
            {
                var temporary_extract = split_for_value[1].Split('=')[0];
                var possibly_value = temporary_extract.Split('%');
                value = double.Parse(possibly_value[0],NumberStyles.Any, CultureInfo);

                if (possibly_value.Length > 1)
                {
                    event_volume = double.Parse(possibly_value[1], NumberStyles.Any, CultureInfo);
                }
            }
            
            // Case !event@16@x
            if (split_for_value.Length > 2)
            {
                var temporary_split = split_for_value[2].Split('=')[0];
                var possibly_value = temporary_split.Split('%');
                scale = possibly_value[0] switch
                {
                    "x" => ValueScale.Times, "+" => ValueScale.Add, "/" => ValueScale.Divide, _ => ValueScale.None
                };
                
                if (possibly_value.Length > 1)
                {
                    event_volume = double.Parse(possibly_value[1],NumberStyles.Any, CultureInfo);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e + $"\n{text}");
            throw;
        }

        var sound = (split_for_repeats.Length > 1 ? split_for_repeats[0].Split("@")[0] : split_for_value[0]).Trim();
        if (split_for_repeats.Length > 1) loop_times = (int)Math.Floor(double.Parse(split_for_repeats.Last(),NumberStyles.Any, CultureInfo));
            
        if ((sound == "_pause" && text.Contains('=')) ||
            (sound == "!stop" && text.Contains('@')) ||
            sound is "!loop" or "!loopmany")
        {
            loop_times = (int)(value > 0 ? value : loop_times);
        }

        if (sound is "_pause" or "!stop" && value == 0)
        {
            value = 1;
        }
            
        var new_event = new Event
        {
            Value = value,
            SoundEvent = sound,
            PlayTimes = loop_times,
            OriginalLoop = loop_times,
            ValueScale = scale,
            Volume = event_volume
        };

        return new_event;
    }

    public static Event ParseColorEvent(string text)
    {
        var split_for_value = text.Split('@');
        var action = split_for_value[0];
        double value;

        if (split_for_value.Length < 2)
        {
            throw new Exception("A color event doesn't have a proper format. The format must be: \'!event@#rgb_or_num, number\'");
        }

        var important_text = split_for_value[1];
        var split_data = important_text.Split(',');

        if (split_data.Length < 2)
        {
            throw new Exception("A color event doesn't have a proper format. The format must be: \'!event@#rgb_or_num, number\'");
        }
        
        if (action is "!bg")
        {
            // Yes I created a custom encoding format for a RGB color and a decimal number. sue me
            var hex = split_data[0];
            byte r, g, b;
            try
            {
                r = Convert.ToByte(hex[1..3], 16);
                g = Convert.ToByte(hex[3..5], 16);
                b = Convert.ToByte(hex[5..7], 16);
            }
            catch (Exception e)
            {
                throw new Exception($"Parsing RGB colors in \'!bg\' action failed: \'{e}\'");
            }

            if (!double.TryParse(split_data[1], NumberStyles.Any, CultureInfo, out var parsed_fade_time))
                throw new Exception("Unable to parse fade time.");

            // Prepare fade time for encode
            var fade_time = TruncateToThreeDecimals(parsed_fade_time);
            
            // Make the number take up only 21 bits.
            var encoded_fade_time = (long) Math.Clamp(fade_time * 1000, 0, 128000);
            
            // Encode the value.
            var value_holder = encoded_fade_time << 24 | (uint) (b << 16) | (uint) (g << 8) | r; 
            value = value_holder;
        }
        else // Action is pulse.
        {
            var count = split_data[0];
            if (!double.TryParse(count, NumberStyles.Any, CultureInfo, out var pulse_times)) 
                throw new Exception("Unable to parse \'!pulse\' action pulses.");

            var frequency = split_data[1];
            if (!double.TryParse(frequency, NumberStyles.Any, CultureInfo, out var repeats)) 
                throw new Exception("Unable to parse \'!pulse\' action pulses.");

            // No need to store decimal places as the site ignores them anyways.
            var r_byte = (byte) repeats;
            var p_short = (short) pulse_times;

            value = p_short << 8 | r_byte;
        }
        
        var new_event = new Event
        {
            Value = value,
            SoundEvent = action,
            PlayTimes = 1,
            OriginalLoop = 1,
            ValueScale = ValueScale.None,
            Volume = null
        };
        return new_event;
    }
    
    private static double TruncateToThreeDecimals(double d)
    {
        return Math.Truncate(d * 1000) / 1000;
    }
}