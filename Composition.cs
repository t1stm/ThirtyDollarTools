using System;
using System.Collections.Generic;
using System.Linq;

namespace ThirtyDollarWebsiteConverter
{
    public class Composition
    {
        public List<Event> Events { get; } = new()
        {
            new Event
            {
                SoundEvent = SoundEvent.Speed,
                Value = 300,
                Loop = 1,
                ValueScale = ValueScale.None
            }
        };
        
        public static Composition FromString(string data)
        {
            var comp = new Composition();
            var split = data.Split('|');

            for (var index = 0; index < split.Length; index++)
            {
                var text = split[index];
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
                var yes = splitForRepeats.Length > 1 ? splitForRepeats[0].Split("@")[0] : splitForValue[0];
                if (splitForRepeats.Length > 1) loopTimes = int.Parse(splitForRepeats.Last());
                var sound = Sounds.FromString(yes);
                if (sound == SoundEvent.Pause && text.Contains('=')) loopTimes = (int) (value > 0 ? value : loopTimes);
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