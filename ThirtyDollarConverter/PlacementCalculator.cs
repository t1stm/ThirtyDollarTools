using System;
using System.Collections.Generic;
using System.Linq;
using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;

namespace ThirtyDollarConverter;

internal enum EventType
{
    Action,
    Sound
}

public class PlacementCalculator
{
    private static readonly string[] jump_untriggers = ["!loop", "!loopmany", "!jump", "!target"];
    private static readonly string[] loop_untriggers = ["!loopmany", "!loop"];
    private static readonly string[] loopmany_untriggers = ["!loopmany"];

    /// <summary>
    ///     Creates a calculator that gets the placement of a sequence.
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
        AddVisualTimings = encoderSettings.AddVisualEvents;
    }

    private uint SampleRate { get; }
    private Action<string> Log { get; }
    private Action<ulong, ulong> IndexReport { get; }
    private bool AddVisualTimings { get; }

    /// <summary>
    ///     Calculates the placement of multiple sequences.
    /// </summary>
    /// <param name="sequences">The sequences you want to calculate.</param>
    /// <returns>The calculated single placement.</returns>
    /// <exception cref="Exception">Exception thats thrown when a sequence has a problem.</exception>
    public IEnumerable<Placement> CalculateMany(IEnumerable<Sequence> sequences)
    {
        var list = new List<Placement>();

        var last_end_index = 0ul;
        foreach (var sequence in sequences)
        {
            var calculated = last_end_index == 0ul ? CalculateOne(sequence) : CalculateOne(sequence, last_end_index);

            var placements = calculated.ToList();
            var last = placements.Last();
            last_end_index = last.Index;
            list.AddRange(placements);
        }

        return list;
    }

    /// <summary>
    ///     Calculates the placement of a sequence.
    /// </summary>
    /// <param name="sequence">The sequence you want to calculate.</param>
    /// <param name="start_time">Optional start time offset.</param>
    /// <returns>The calculated placement.</returns>
    /// <exception cref="Exception">Exception thats thrown when the sequence has a problem.</exception>
    public IEnumerable<Placement> CalculateOne(Sequence sequence, ulong? start_time = null)
    {
        if (sequence == null) throw new Exception("Null Sequence");
        sequence = sequence.Copy();
        
        var bpm = 300.0;
        var transpose = 0.0;
        var global_volume = 100.0;
        var count = (ulong)sequence.Events.LongLength;
        var position = start_time ?? (ulong)(SampleRate / (bpm / 60));

        // I have given up on reverse engineering my own parser.
        // Here goes GD Colon's code.
        // - t1stm

        var scrub_pos = 0ul;
        var loop_target = 0ul;
        var index = 0ul;
        var scrubbing = false;

        while (index < count)
        {
            var ev = sequence.Events[index];
            IndexReport(index, count);
            var event_type = (ev.SoundEvent?.StartsWith('!') ?? true) || ev is ICustomActionEvent
                ? EventType.Action
                : EventType.Sound;

            var increment_timer = false;
            var modify_index = true;

            if (scrubbing && index == scrub_pos) scrubbing = false;

            if (event_type == EventType.Sound)
            {
                var next_event = index + 1 < count ? sequence.Events[index + 1].SoundEvent : null;
                increment_timer = next_event is not "!combine";

                var copy = ev.Copy();
                var event_volume = copy.Volume ??= 100;
                copy.WorkingVolume = global_volume * event_volume / 100d;

                copy.Value += transpose;
                var placement = new Placement
                {
                    Index = position,
                    SequenceIndex = index,
                    Event = copy,
                    Audible = ev.SoundEvent is not "_pause"
                };

                if (!scrubbing) yield return placement;
                if (increment_timer) position += (ulong)(SampleRate / (bpm / 60));
                index++;
                continue;
            }

            var audible = ev is ICustomActionEvent and ICustomAudibleEvent;
            var default_return = true;

            switch (ev.SoundEvent)
            {
                case "!speed":
                {
                    switch (ev.ValueScale)
                    {
                        case ValueScale.Divide:
                            bpm /= ev.Value;
                            break;
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
                }

                case "!volume":
                {
                    switch (ev.ValueScale)
                    {
                        case ValueScale.Divide:
                            global_volume /= ev.Value;
                            break;
                        case ValueScale.Times:
                            global_volume *= ev.Value;
                            break;
                        case ValueScale.Add:
                            global_volume += ev.Value;
                            break;
                        case ValueScale.None:
                            global_volume = ev.Value;
                            break;
                    }

                    if (global_volume < 0) global_volume = 0;
                    default_return = false;

                    var copy = ev.Copy();
                    copy.WorkingVolume = global_volume;

                    yield return new Placement
                    {
                        Index = position,
                        SequenceIndex = index,
                        Event = copy,
                        Audible = false
                    };
                    break;
                }

                case "!stop":
                {
                    var working_value = ev.Value;
                    while (ev.PlayTimes > 0)
                    {
                        var multiplier = Math.Min(working_value, 1);
                        position += (ulong)(multiplier * SampleRate / (bpm / 60));

                        ev.PlayTimes -= 1;
                        working_value -= 1;

                        if (ev.PlayTimes < 0)
                            ev.PlayTimes = 0;

                        if (AddVisualTimings)
                            yield return new Placement
                            {
                                Index = position,
                                SequenceIndex = index,
                                Event = ev.Copy(),
                                Audible = false
                            };
                    }

                    break;
                }

                case "!loopmany":
                {
                    if (ev.PlayTimes > 0)
                    {
                        default_return = false;
                        ev.PlayTimes--;

                        modify_index = false;

                        yield return new Placement
                        {
                            Index = position,
                            SequenceIndex = index,
                            Event = ev.Copy(),
                            Audible = false
                        };

                        index = loop_target;

                        Untrigger(ref sequence, index, loopmany_untriggers);
                        Log($"Going to element: ({index}) - \"{sequence.Events[index]}\"");
                    }

                    break;
                }

                case "!loop":
                {
                    if (!ev.Triggered)
                    {
                        ev.Triggered = true;
                        default_return = false;
                        yield return new Placement
                        {
                            Index = position,
                            SequenceIndex = index,
                            Event = ev,
                            Audible = false
                        };

                        modify_index = false;
                        index = loop_target;

                        Untrigger(ref sequence, index, loop_untriggers);
                        Log($"Going to element: ({index}) - \"{sequence.Events[index]}\"");
                    }

                    break;
                }

                case "!jump":
                {
                    if (ev.PlayTimes <= 0) break;
                    ev.PlayTimes--;

                    var item = sequence.Events.FirstOrDefault(r =>
                        r.SoundEvent == "!target" && Math.Abs(r.Value - ev.Value) < 0.001f && r.Triggered == false);
                    if (item == null)
                    {
                        Log($"Unable to jump to target with id: {ev.Value}");
                        break;
                    }

                    var search = Array.IndexOf(sequence.Events, item);
                    if (search == -1)
                    {
                        Untrigger(ref sequence, 0, jump_untriggers);
                        break;
                    }

                    default_return = false;
                    yield return new Placement
                    {
                        Index = position,
                        SequenceIndex = index,
                        Event = ev,
                        Audible = false
                    };

                    modify_index = false;
                    index = (ulong)search;
                    var found_event = sequence.Events[index];

                    Untrigger(ref sequence, index, jump_untriggers);
                    Log($"Jumping to element: ({index}) - {found_event}");
                    break;
                }

                case "!cut":
                {
                    audible = true;
                    Log($"Cutting audio at: \'{position + SampleRate / (bpm / 60)}\'");
                    break;
                }

                case "!looptarget":
                {
                    loop_target = index;
                    break;
                }
                
                case "" or "!flash" or "!bg" or "!combine" or "!startpos" or "!pulse" or "!target":
                    break;

                case "!transpose":
                {
                    switch (ev.ValueScale)
                    {
                        case ValueScale.Divide:
                            transpose /= ev.Value;
                            break;
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
            }

            if (!scrubbing && default_return)
                yield return new Placement
                {
                    Index = position,
                    SequenceIndex = index,
                    Event = ev,
                    Audible = audible
                };
            if (modify_index) index++;
            if (!scrubbing && increment_timer) position += (ulong)(SampleRate / (bpm / 60));
        }

        yield return new Placement
        {
            Index = position,
            SequenceIndex = index,
            Event = new EndEvent(),
            Audible = false
        };
    }

    /// <summary>
    ///     Method ported from GD Colon's site. Untriggers all samples from the starting index to the end.
    /// </summary>
    /// <param name="sequence">Reference to the sequence.</param>
    /// <param name="index">The index to start from.</param>
    /// <param name="except">An array of strings containing events to ignore.</param>
    private static void Untrigger(ref Sequence sequence, ulong index, string[] except)
    {
        if (index == 0) index++;
        for (var i = index - 1; i < (ulong)sequence.Events.LongLength; i++)
        {
            var current_event = sequence.Events[i];
            if (except.Any(r => r == current_event.SoundEvent)) continue;

            current_event.Triggered = false;
            current_event.PlayTimes = current_event.OriginalLoop;
        }
    }
}