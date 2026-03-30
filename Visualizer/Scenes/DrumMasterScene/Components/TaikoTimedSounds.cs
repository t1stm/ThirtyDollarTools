using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;

namespace DrumMasterScene.Components;

public class TaikoTimedSounds
{
    public List<TaikoSound> Sounds { get; set; }
    public float BaseScrollSpeed { get; set; } = 0.66f;

    public TaikoTimedSounds(Placement[] placements, TaikoSoundMap soundMap, double sampleRate = 100_000, bool useBPMForScrollSpeed = true)
    {
        Sounds = [];
        var bpm = 300d; // default BPM in Placements
        var highSpeedFactor = 1d;
        var lastTime = 0d;
        
        foreach (var placement in placements)
        {
            var ev = placement.Event;
            if (ev.SoundEvent == "!speed")
            {
                switch (ev.ValueScale)
                {
                    case ValueScale.Divide: bpm /= ev.Value; break;
                    case ValueScale.Times: bpm *= ev.Value; break;
                    case ValueScale.Add: bpm += ev.Value; break;
                    case ValueScale.None: bpm = ev.Value; break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                continue;
            }

            if (ev.SoundEvent == "!highspeed")
            {
                highSpeedFactor = ev.Value;
                continue;
            }

            var soundName = ev.SoundEvent ?? "";
            if (!soundMap.Has(soundName)) continue;

            var timestamp = placement.Index * 1000 / sampleRate;
            var scrollSpeed = highSpeedFactor * (useBPMForScrollSpeed ? BaseScrollSpeed * (bpm / 300d) : BaseScrollSpeed);
            
            if (Math.Abs(timestamp - lastTime) < double.Epsilon) continue;
            lastTime = timestamp;

            var panText = string.Empty;
            if (ev is PannedEvent pannedEvent)
            {
                var panString = Math.Abs(pannedEvent.TDWPan).ToString("0.##");
                panText = pannedEvent.Pan > 0
                    ? $"{panString}>"
                    : $"<{panString}";
            }

            Sounds.Add(new TaikoSound
            {
                Event = ev,
                Sound = soundName,
                Volume = ev.Volume is not null ? $"{ev.Volume:0.##}%" : "",
                Pan = panText,
                Timestamp = timestamp,
                ScrollSpeed = scrollSpeed,
            });
        }
    }
}