namespace ThirtyDollarParser;

public class Composition
{
    public Event[] Events { get; set; } = Array.Empty<Event>();

    public Composition Copy()
    {
        return new Composition
        {
            Events = Events.Select(r => r.Copy()).ToArray()
        };
    }

    /// <summary>
    /// Parses a composition stored in a string.
    /// </summary>
    /// <param name="data">The string containing the composition.</param>
    /// <returns>The parsed composition.</returns>
    public static Composition FromString(string data)
    {
        var comp = new Composition();
        var split = data.Replace("!divider", "").Split('|');
        var list = new List<Event>();
        foreach (var originalText in split)
        {
            var text = originalText;
            if (string.IsNullOrEmpty(text) || text is "\n") continue;
            if (text.StartsWith("!pulse") || text.StartsWith("!bg") || text.StartsWith("!flash"))
                // Skipping color line due to no current support.
                continue;

            if (text[1..].Any(ch => ch == '!'))
                // Text contains more than one parameter on event without divider. Adding the first event only.
                text = text[..text[1..].IndexOf('!')];

            var new_event = ParseEvent(text);
            list.Add(new_event);
        }

        comp.Events = list.ToArray();
        return comp;
    }

    /// <summary>
    /// Parses a single event.
    /// </summary>
    /// <param name="text">The string of the event.</param>
    /// <returns>The parsed event.</returns>
    private static Event ParseEvent(string text)
    {
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
                value = double.Parse(possibly_value[0]);

                if (possibly_value.Length > 1)
                {
                    event_volume = double.Parse(possibly_value[1]);
                }
            }
            
            // Case !event@16@x
            if (split_for_value.Length > 2)
            {
                var temporary_split = split_for_value[2].Split('=')[0];
                var possibly_value = temporary_split.Split('%');
                scale = possibly_value[0] switch
                {
                    "x" => ValueScale.Times, "+" => ValueScale.Add, _ => ValueScale.None
                };
                
                if (possibly_value.Length > 1)
                {
                    event_volume = double.Parse(possibly_value[1]);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e + $"\n{text}");
            throw;
        }

        var sound = (split_for_repeats.Length > 1 ? split_for_repeats[0].Split("@")[0] : split_for_value[0]).Trim();
        if (split_for_repeats.Length > 1) loop_times = (int)Math.Floor(double.Parse(split_for_repeats.Last()));
            
        if ((sound == "_pause" && text.Contains('=')) ||
            (sound == "!stop" && text.Contains('@')) ||
            sound is "!loop" or "!loopmany")
        {
            loop_times = (int)(value > 0 ? value : loop_times);
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
}