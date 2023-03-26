namespace ThirtyDollarParser;

public class Composition
{
    public Event[] Events { get; set; } =
    {
        new()
        {
            SoundEvent = "!speed",
            Value = 300,
            PlayTimes = 1,
            ValueScale = ValueScale.None
        }
    };

    public Composition Copy()
    {
        return new Composition
        {
            Events = Events.Select(r => r.Copy()).ToArray()
        };
    }

    public static Composition FromString(string data)
    {
        var comp = new Composition();
        var split = data.Replace("!divider", "").Split('|');
        var list = new List<Event>();
        foreach (var originalText in split)
        {
            var text = originalText;
            if (string.IsNullOrEmpty(text)) continue;
            if (text.StartsWith("!pulse") || text.StartsWith("!bg") || text.StartsWith("!flash"))
                // Skipping color line due to no current support.
                continue;

            if (text[1..].Count(ch => ch == '!') > 0)
                // Text contains more than one parameter on event without divider. Adding the first event only.
                text = text[..text[1..].IndexOf('!')];
            var splitForValue = text.Split('@');
            var splitForRepeats = text.Split('=');
            var value = 0.0;
            var scale = ValueScale.None;
            var loopTimes = 1;
            try
            {
                if (splitForValue.Length > 1) value = double.Parse(splitForValue[1].Split('=')[0]);
                if (splitForValue.Length > 2)
                {
                    var scaleString = splitForValue[2].Split('=');
                    scale = scaleString[0] switch
                    {
                        "x" => ValueScale.Times, "+" => ValueScale.Add, _ => ValueScale.None
                    };
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e + $"\n{text}");
                throw;
            }

            var sound = (splitForRepeats.Length > 1 ? splitForRepeats[0].Split("@")[0] : splitForValue[0]).Trim();
            if (splitForRepeats.Length > 1) loopTimes = (int)Math.Floor(double.Parse(splitForRepeats.Last()));
            if ((sound == "_pause" && text.Contains('=')) || (sound == "!stop" && text.Contains('@')))
                loopTimes = (int)(value > 0 ? value : loopTimes);
            var newEvent = new Event
            {
                Value = value,
                SoundEvent = sound,
                PlayTimes = loopTimes,
                OriginalLoop = loopTimes,
                ValueScale = scale
            };
            list.Add(newEvent);
        }

        comp.Events = list.ToArray();
        return comp;
    }
}