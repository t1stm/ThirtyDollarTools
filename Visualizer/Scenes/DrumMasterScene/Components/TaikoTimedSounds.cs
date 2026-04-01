using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;

namespace DrumMasterScene.Components;

public class TaikoTimedSounds
{
    public List<TaikoSound> Sounds { get; set; }
    public float BaseScrollSpeed { get; set; } = 0.5f;

    public TaikoTimedSounds(Placement[] placements, TaikoSoundMap soundMap, double sampleRate = 100_000,
        bool useBPMForScrollSpeed = true)
    {
        Sounds = [];
        var bpm = 300d; // default BPM in Placements
        var highSpeedFactor = 1d;
        var lastTime = 0d;

        foreach (var placement in placements)
        {
            var ev = placement.Event;
            switch (ev.SoundEvent)
            {
                case "!speed":
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
                case "!highspeed":
                    highSpeedFactor = ev.Value;
                    continue;
            }

            var soundName = ev.SoundEvent ?? "";
            if (!soundMap.Has(soundName)) continue;

            var timestamp = placement.Index * 1000 / sampleRate;
            var scrollSpeed = highSpeedFactor *
                              (useBPMForScrollSpeed ? BaseScrollSpeed * (bpm / 300d) : BaseScrollSpeed);

            if (Math.Abs(timestamp - lastTime) < double.Epsilon) continue;
            lastTime = timestamp;

            Sounds.Add(new TaikoSound
            {
                Event = ev,
                Timestamp = timestamp,
                ScrollSpeed = scrollSpeed,
            });
        }
    }
}