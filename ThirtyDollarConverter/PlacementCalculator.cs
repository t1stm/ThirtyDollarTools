using System;
using System.Collections.Generic;
using System.Linq;
using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;

namespace ThirtyDollarConverter;

enum EventType
{
    Action,
    Sound
}

public class PlacementCalculator
{
    private uint SampleRate { get; }
    private Action<string> Log { get; }
    private Action<ulong, ulong> IndexReport { get; }

    /// <summary>
    /// Creates a calculator that gets the placement of a composition.
    /// </summary>
    /// <param name="encoderSettings">Encoder settings to base the placement on.</param>
    /// <param name="log">Action that handles log messages.</param>
    /// <param name="indexReport">Action that recieves encode progress.</param>
    public PlacementCalculator(EncoderSettings encoderSettings, Action<string>? log = null,
        Action<ulong, ulong>? indexReport = null)
    {
        Log = log ?? (_ => { });
        IndexReport = indexReport ?? ((_, _) => { });
        SampleRate = encoderSettings.SampleRate;
    }

    /// <summary>
    /// Calculates the placement of a composition.
    /// </summary>
    /// <param name="composition">The composition you want to calculate.</param>
    /// <returns>The calculated placement.</returns>
    /// <exception cref="Exception">Exception thats thrown when the composition has a problem.</exception>
    public IEnumerable<Placement> Calculate(Composition composition)
    {
        if (composition == null) throw new Exception("Null Composition");
        var bpm = 300.0;
        var transpose = 0.0;
        var volume = 100.0;
        var count = (ulong)composition.Events.LongLength;
        var position = (ulong)(SampleRate / (bpm / 60));

        // I have given up on reverse engineering my own parser.
        // Here goes GD Colon's code.
        // - t1stm

        var scrub_pos = 0ul;
        var loop_target = 0ul;
        var index = 0ul;
        var scrubbing = false;

        while (index < count)
        {
            var ev = composition.Events[index];
            IndexReport(index, count);
            var event_type = ev.SoundEvent is "_pause" || (ev.SoundEvent?.StartsWith('!') ?? true) ? EventType.Action : EventType.Sound;
            var increment_timer = false;
            var modify_index = true;

            if (scrubbing && index == scrub_pos) scrubbing = false;

            if (event_type == EventType.Sound)
            {
                var next_event = index + 1 < count ? composition.Events[index + 1].SoundEvent : null;
                increment_timer = next_event is not "!combine";
                
                var copy = ev.Copy();
                copy.Volume ??= volume;
                copy.Value += transpose;
                var placement = new Placement
                {
                    Index = position,
                    Event = copy
                };
                
                if (!scrubbing) yield return placement;

                if (ev.PlayTimes > 1)
                {
                    increment_timer = true;
                    index--;
                }
                
                if (increment_timer) position += (ulong)(SampleRate / (bpm / 60));
                
                ev.PlayTimes--;
                index++;
                continue;
            }

            switch (ev.SoundEvent)
            {
                case "!speed":
                    switch (ev.ValueScale)
                    {
                        case ValueScale.Times:
                            bpm *= ev.Value;
                            break;
                        case ValueScale.Add:
                            bpm += ev.Value;
                            break;
                        case ValueScale.None:
                            bpm = ev.Value;
                            break;
                    }

                    Log($"BPM is now: {bpm}");
                    break;

                case "!volume":
                    switch (ev.ValueScale)
                    {
                        case ValueScale.Times:
                            volume *= ev.Value;
                            break;
                        case ValueScale.Add:
                            volume += ev.Value;
                            break;
                        case ValueScale.None:
                            volume = ev.Value;
                            break;
                    }

                    break;
                
                case "_pause": 
                case "!stop":
                    while (ev.PlayTimes > 0)
                    {
                        position += (ulong)(SampleRate / (bpm / 60));

                        ev.PlayTimes--;
                        if (ev.PlayTimes < 0) ev.PlayTimes = 0;
                    }
                    break;
                    
                case "!loopmany":
                    if (ev.PlayTimes > 0)
                    {
                        ev.PlayTimes--;
                        if (loop_target == 0)
                        {
                            loop_target = 1;
                            modify_index = false;
                        }
                        index = loop_target - 1;
                            
                        Untrigger(composition, index, new[] { "!loopmany" });
                        Log($"Going to element: ({index}) - \"{composition.Events[index]}\"");
                    }
                    break;
                    
                case "!loop":
                    if (!ev.Triggered)
                    {
                        ev.Triggered = true;
                        if (loop_target == 0)
                        {
                            loop_target = 1;
                            modify_index = false;
                        }
                        index = loop_target - 1;
                            
                        Untrigger(composition, index, new[] { "!loopmany", "!loop" });
                        Log($"Going to element: ({index}) - \"{composition.Events[index]}\"");
                    }
                    break;

                case "!jump":
                    if (ev.PlayTimes <= 0) break;
                    ev.PlayTimes--;
                        
                    var item = composition.Events.FirstOrDefault(r =>
                        r.SoundEvent == "!target" && (int) r.Value == (int) ev.Value && r.Triggered == false);
                    if (item == null)
                    {
                        Log($"Unable to jump to target with id: {ev.Value}");
                        break;
                    }

                    var search = Array.IndexOf(composition.Events, item);
                    if (search == -1)
                    {
                        Untrigger(composition, 0, new[] { "!loop", "!loopmany", "!jump", "!target" });
                        break;
                    }

                    index = (ulong) search;
                    var found_event = composition.Events[index];

                    Untrigger(composition, index, new[] { "!loop", "!loopmany", "!jump", "!target" });
                    Log($"Jumping to element: ({index}) - {found_event}");
                    break;

                case "!cut":
                    yield return new Placement
                    {
                        Event = new Event
                        {
                            SoundEvent = "#!cut",
                            Value = position + SampleRate / (bpm / 60)
                        },
                        Index = position
                    };
                    Log($"Cutting audio at: \'{position + SampleRate / (bpm / 60)}\'");
                    break;

                case "!looptarget":
                    loop_target = index;
                    break;
                    
                case "!target":
                    break;
                
                case "" or "!volume" or "!flash" or "!bg" or "!combine" or "!startpos":
                    break;

                case "!transpose":
                    switch (ev.ValueScale)
                    {
                        case ValueScale.Times:
                            transpose *= ev.Value;
                            break;
                        case ValueScale.Add:
                            transpose += ev.Value;
                            break;
                        case ValueScale.None:
                            transpose = ev.Value;
                            break;
                    }

                    Log($"Transposing samples by: \'{transpose}\'");
                    break;
            }

            if (modify_index) index++;
            if (!scrubbing && increment_timer) position += (ulong) (SampleRate / (bpm / 60));
        }
    }

    private static void Untrigger(Composition composition, ulong index, string?[] except)
    {
        for (var i = index - 1; i < (ulong) composition.Events.LongLength; i++)
        {
            var current_event = composition.Events[i];
            if (except.Any(r => r == current_event.SoundEvent)) continue;

            current_event.Triggered = false;
            current_event.PlayTimes = current_event.OriginalLoop;
        }
    }
}