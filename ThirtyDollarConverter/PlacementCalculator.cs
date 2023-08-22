using System;
using System.Collections.Generic;
using System.Linq;
using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;

namespace ThirtyDollarConverter;

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
        Log = log ?? (_ => {});
        IndexReport = indexReport ?? ((_, _) => {});
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
        var position = (ulong)(SampleRate / (bpm / 60));
        var transpose = 0.0;
        var volume = 100.0;
        var count = (ulong) composition.Events.LongLength;

        var is_previous_settings_event = false;

        for (var i = 0ul; i < count; i++)
        {
            var index = position;
            var ev = composition.Events[i];
            IndexReport(i, count);
            switch (ev.SoundEvent)
            {
                case "!speed":
                    is_previous_settings_event = true;
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
                    continue;

                case "!volume":
                    is_previous_settings_event = true;
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

                    //Log($"Changing sample volume to: \'{volume}\'");
                    continue;

                case "!loopmany" or "!loop":
                    is_previous_settings_event = true;
                    if (ev.PlayTimes <= 0) continue;
                    ev.PlayTimes--;

                    var old_i = i;
                    
                    for (var j = i; j > 0; j--)
                    {
                        if (composition.Events[j].SoundEvent != "!looptarget") continue;

                        i = j - 1;
                        break;
                    }

                    if (i == old_i)
                    {
                        i = 0;
                        position += (ulong)(SampleRate / (bpm / 60));
                    }

                    Log($"Going to element: ({i}) - \"{composition.Events[i]}\"");
                    continue;

                case "!jump":
                    is_previous_settings_event = true;
                    if (ev.PlayTimes <= 0) continue;
                    ev.PlayTimes--;
                    //i = Triggers[(int) ev.Value - 1] - 1;
                    var item = composition.Events.FirstOrDefault(r =>
                        r.SoundEvent == "!target" && (int)r.Value == (int)ev.Value);
                    if (item == null)
                    {
                        Log($"Unable to jump to target with id: {ev.Value}");
                        continue;
                    }

                    var search = Array.IndexOf(composition.Events, item);
                    if (search == -1)
                    {
                        Log("Unable to find event: ");
                        continue;
                    }
                    
                    i = (ulong) search;
                    var found_event = composition.Events[i];
                    
                    Log($"Jumping to element: ({i}) - {found_event}");

                    continue;

                case "_pause" or "!stop":
                    Log($"Pausing for: \'{ev.PlayTimes}\' beats");
                    while (ev.PlayTimes >= 1)
                    {
                        ev.PlayTimes--;
                        position += (ulong)(SampleRate / (bpm / 60));
                    }

                    ev.PlayTimes = ev.OriginalLoop;
                    continue;

                case "!cut":
                    is_previous_settings_event = true;
                    yield return new Placement
                    {
                        Event = new Event
                        {
                            SoundEvent = "#!cut",
                            Value = index + SampleRate / (bpm / 60)
                        },
                        Index = index
                    };
                    Log($"Cutting audio at: \'{index + SampleRate / (bpm / 60)}\'");
                    continue;

                case "" or "!looptarget" or "!target" or "!volume" or "!flash" or "!bg":
                    is_previous_settings_event = true;
                    continue;

                case "!combine":
                    if (is_previous_settings_event) continue;
                    
                    is_previous_settings_event = true;
                    position -= (ulong)(SampleRate / (bpm / 60));
                    continue;

                case "!transpose":
                    is_previous_settings_event = true;
                    switch (ev.ValueScale)
                    {
                        case ValueScale.Times:
                            transpose *= ev.Value;
                            continue;
                        case ValueScale.Add:
                            transpose += ev.Value;
                            continue;
                        case ValueScale.None:
                            transpose = ev.Value;
                            continue;
                    }
                    
                    Log($"Transposing samples by: \'{transpose}\'");
                    continue;

                default:
                    position += (ulong)(SampleRate / (bpm / 60));
                    break;
            }

            // To avoid modifying the original event.
            var copy = ev.Copy();
            copy.Volume ??= volume;
            copy.Value += transpose;
            var placement = new Placement
            {
                Index = index,
                Event = copy
            };
            //Log($"Found placement of event: \'{placement.Event}\'");
            yield return placement;
            switch (ev.SoundEvent)
            {
                case not ("!transpose" or "!loopmany" or "!volume" or "!flash" or "!combine" or "!speed" or
                    "!looptarget" or "!loop" or "!cut" or "!target" or "!jump" or "_pause" or "!stop"):
                    if (ev.PlayTimes > 1)
                    {
                        ev.PlayTimes--;
                        i--;
                        continue;
                    }

                    ev.PlayTimes = ev.OriginalLoop;
                    is_previous_settings_event = false;
                    continue;
            }
        }
    }
}