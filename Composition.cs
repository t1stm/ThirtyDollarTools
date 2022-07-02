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

            foreach (var text in split)
            {
                var splitForPitch = text.Split('@');
                var splitForRepeats = text.Split('=');
                var value = 0.0;
                var scale = ValueScale.None;
                try
                {
                    if (splitForPitch.Length > 1) value = double.Parse(splitForPitch[1].Split('=')[0]);
                    if (splitForPitch.Length > 2)
                    {
                        scale = splitForPitch[1] switch {"x" => ValueScale.Times, "+" => ValueScale.Add, _ => ValueScale.None};
                        Console.WriteLine($"Setting pitch to: {scale}");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e + $"\n{text}");
                    throw;
                }
                
                var times = 1;
                var yes = splitForRepeats.Length > 1 ? splitForRepeats[0].Split("@")[0] : splitForPitch[0];
                if (splitForRepeats.Length > 1) times = int.Parse(splitForRepeats.Last());
                var newEvent = new Event
                {
                    Value = value,
                    SoundEvent = Sounds.FromString(yes),
                    Loop = times,
                    ValueScale = scale
                };
                comp.Events.Add(newEvent);
            }
            
            return comp;
        }
    }
}