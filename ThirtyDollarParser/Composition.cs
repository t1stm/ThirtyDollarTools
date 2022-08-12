using System;
using System.Collections.Generic;
using System.Linq;

namespace ThirtyDollarParser
{
    public class Composition
    {
        public List<Event> Events { get; } = new()
        {
            new Event
            {
                SoundEvent = "!speed",
                Value = 300,
                Loop = 1,
                ValueScale = ValueScale.None
            }
        };

        public static Composition FromString(string data)
        {
            var comp = new Composition();
            var split = data.Replace("!divider", "").Split('|');
            foreach (var orText in split)
            {
                var text = orText;
                if (string.IsNullOrEmpty(text)) continue;
                if (text.StartsWith("!pulse") || text.StartsWith("!bg") || text.StartsWith("!flash"))
                {
                    // Skipping color line due to no current support.
                    continue;
                }

                if (text[1..].Count(ch => ch == '!') > 0)
                {
                    // Text contains more than one parameter on event without divider. Adding the first event only.
                    text = text[..text[1..].IndexOf('!')]; 
                }
                var splitForValue = text.Split('@');
                var splitForRepeats = text.Split('=');
                var value = 0.0;
                var scale = ValueScale.None;
                try
                {
                    if (splitForValue.Length > 1) value = double.Parse(splitForValue[1].Split('=')[0]);
                    if (splitForValue.Length > 2)
                    {
                        scale = splitForValue[2] switch
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

                var loopTimes = 1;
                var sound = (splitForRepeats.Length > 1 ? splitForRepeats[0].Split("@")[0] : splitForValue[0]).Trim();
                if (splitForRepeats.Length > 1) loopTimes = int.Parse(splitForRepeats.Last());
                if (sound == "_pause" && text.Contains('=') || sound == "!stop" && text.Contains('@')) loopTimes = (int) (value > 0 ? value : loopTimes);
                var newEvent = new Event
                {
                    Value = value,
                    SoundEvent = sound,
                    Loop = loopTimes,
                    OriginalLoop = loopTimes,
                    ValueScale = scale
                };
                comp.Events.Add(newEvent);
            }

            return comp;
        }
    }
}