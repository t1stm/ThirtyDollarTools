using System.Globalization;
using System.Text.RegularExpressions;
using ThirtyDollarParser.Custom_Events;

namespace ThirtyDollarParser;

public partial class Sequence
{
    private static readonly CultureInfo CultureInfo = CultureInfo.InvariantCulture;
    public Dictionary<string, BaseEvent[]> Definitions = new();
    public HashSet<string> SeparatedChannels = [];
    public HashSet<string> UsedSounds = [];
    public BaseEvent[] Events { get; set; } = [];

    public Sequence Copy()
    {
        return new Sequence
        {
            Events = Events.Select(r => r.Copy()).ToArray(),
            Definitions = Definitions,
            SeparatedChannels = SeparatedChannels
        };
    }
    
    /// <summary>
    /// Parses a sequence stored in a string.
    /// </summary>
    /// <param name="data">The string containing the sequence.</param>
    /// <returns>The parsed sequence.</returns>
    public static Sequence FromString(string data)
    {
        var sequence = new Sequence();
        var split = data.Split('|');
        var list = new List<BaseEvent>();

        using var enumerator = split.AsEnumerable().GetEnumerator();
        while (enumerator.MoveNext())
        {
            var text = enumerator.Current;

            text = text.Replace("\n", "").Trim();
            if (string.IsNullOrEmpty(text)) continue;
            if (text.StartsWith('#'))
                if (TryDefine(text, enumerator, sequence))
                    continue;

            var new_event = ParseEvent(text, sequence);

            if ((new_event.SoundEvent?.StartsWith('!') ?? false) && new_event.SoundEvent is not "!divider")
            {
                list.Add(new_event);
                continue;
            }

            var repeats = new_event.PlayTimes;

            new_event.OriginalLoop = 1;
            new_event.PlayTimes = 1;
            
            if (new_event.SoundEvent is not null)
                sequence.UsedSounds.Add(new_event.SoundEvent);
            
            for (var i = 0; i < repeats; i++)
            {
                if (ProcessDefines(sequence, new_event, list)) continue;
                var copy = new_event.Copy();
                list.Add(copy);
            }
        }

        sequence.Events = list.ToArray();
        return sequence;
    }

    private static bool TryDefine(string text, IEnumerator<string> enumerator, Sequence sequence)
    {
        var special_match = DefineRegex().Match(text);
        if (!special_match.Success) return true;

        if (special_match.Groups["name"].Value != "define") return false;
        if (!enumerator.MoveNext()) return true;

        var defines = ParseDefines(in enumerator, sequence);
        var define_name = special_match.Groups["value"].Value;

        sequence.Definitions.Add(define_name, defines);
        return true;
    }

    private static bool TryIndividualCut(string text, Sequence sequence, out BaseEvent newEvent)
    {
        newEvent = NormalEvent.Empty;

        var match = ICutRegex().Match(text);
        if (!match.Success) return false;
        if (!match.Groups["events"].Success) return false;

        var cut_events = match.Groups["events"].Value;
        var split_events = cut_events.Split(',');

        for (var i = 0; i < split_events.Length; i++)
        {
            var name = split_events[i];
            split_events[i] = name.Trim();
        }

        var hash_set = new HashSet<string>(split_events);

        newEvent = new IndividualCutEvent(hash_set)
        {
            SoundEvent = text
        };

        foreach (var ev in split_events) sequence.SeparatedChannels.Add(ev);

        return true;
    }

    private static bool TryBookmark(string text, out BaseEvent newEvent)
    {
        newEvent = NormalEvent.Empty;

        var match = BookmarkRegex().Match(text);
        if (!match.Success) return false;
        if (!match.Groups["index"].Success) return false;

        var string_value = match.Groups["index"].Value;
        if (!int.TryParse(string_value, CultureInfo, out var val)) return false;

        newEvent = new BookmarkEvent
        {
            Value = val
        };
        return true;
    }

    private static BaseEvent[] ParseDefines(in IEnumerator<string> enumerator, Sequence sequence)
    {
        var events = new List<BaseEvent>();

        while (true)
        {
            var text = enumerator.Current;
            var trimmed = text.Trim();
            if (trimmed == "#enddefine") break;

            var parsed = ParseEvent(trimmed, sequence);
            if (parsed.SoundEvent is null) continue;

            if (!sequence.Definitions.TryGetValue(parsed.SoundEvent, out var defined_events))
            {
                events.Add(parsed);
                if (!enumerator.MoveNext()) break;
                continue;
            }

            var pan = 0f;
            if (parsed is PannedEvent panned) pan = panned.Pan;

            events.AddRange(defined_events.Select(e =>
            {
                switch (e)
                {
                    case ICustomActionEvent custom_action_event:
                        return (BaseEvent)custom_action_event;

                    default:
                    {
                        var panned_event = new PannedEvent
                        {
                            SoundEvent = e.SoundEvent,
                            Value = !(e.SoundEvent ?? "").StartsWith('!') ? e.Value + parsed.Value : 0,
                            Volume = e.Volume * ((parsed.Volume ?? 100) / 100),
                            Pan = (e.SoundEvent ?? "").StartsWith('!') ? 0 : pan + (e is PannedEvent p ? p.Pan : 0),
                            ValueScale = e.ValueScale
                        };

                        return panned_event;
                    }
                }
            }));

            if (!enumerator.MoveNext()) break;
        }

        return events.ToArray();
    }

    private static bool ProcessDefines(Sequence comp, BaseEvent newEvent, List<BaseEvent> list)
    {
        if (!comp.Definitions.TryGetValue(newEvent.SoundEvent ?? "", out var events)) return false;

        var pan = 0f;
        if (newEvent is PannedEvent panned_event) pan = panned_event.Pan;

        var array = new BaseEvent[events.Length];
        for (var i = 0; i < events.Length; i++)
        {
            var base_event = events[i];

            array[i] = base_event switch
            {
                NormalEvent => new PannedEvent(base_event),
                IndividualCutEvent ice => ice.Copy(),
                _ => base_event.Copy()
            };
        }

        if (newEvent is
                { Value: 0, ValueScale: ValueScale.None or ValueScale.Add, Volume: null or 100d } &&
            pan == 0f) goto return_path;

        var val = newEvent.Value;
        foreach (var ev in array)
        {
            if ((ev.SoundEvent?.StartsWith('!') ?? false) || ev is ICustomActionEvent) continue;
            if (ev is PannedEvent panned)
            {
                var new_pan = Math.Clamp(pan + panned.Pan, -1f, 1f);
                panned.Pan = new_pan;
            }

            switch (newEvent.ValueScale)
            {
                case ValueScale.None:
                case ValueScale.Add:
                    ev.Value += val;
                    break;

                case ValueScale.Divide:
                    ev.Value /= val;
                    break;

                case ValueScale.Times:
                    ev.Value *= val;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (newEvent.Volume is null or 100d) continue;

            switch (ev.Volume)
            {
                case null:
                    ev.Volume = newEvent.Volume;
                    break;
                default:
                    ev.Volume *= newEvent.Volume / 100d;
                    break;
            }
        }

        return_path:
        list.AddRange(array);
        return true;
    }
    
    private static bool TryIndividualCutTDW(string text, Sequence sequence, out IndividualCutEvent icut)
    {
        var splitForValue = text.Split('@');
        if (splitForValue is not ["!cut", _])
        {
            icut = new IndividualCutEvent([]);
            return false;
        }

        var cutSounds = splitForValue[1].Split(',').ToHashSet();
        icut = new IndividualCutEvent(cutSounds)
        {
            IsStandardImplementation = true
        };
        
        foreach (var ev in cutSounds) sequence.SeparatedChannels.Add(ev);
        return true;
    }

    /// <summary>
    /// Parses a single event.
    /// </summary>
    /// <param name="text">The string of the event.</param>
    /// <param name="sequence">The sequence that is going to be parsed.</param>
    /// <returns>The parsed event.</returns>
    private static BaseEvent ParseEvent(string text, Sequence sequence)
    {
        if (TryIndividualCut(text, sequence, out var new_individual_cut_event)) return new_individual_cut_event;
        if (TryIndividualCutTDW(text, sequence, out var new_individual_cut_tdw_event)) return new_individual_cut_tdw_event;
        if (TryBookmark(text, out var bookmark_event)) return bookmark_event;

        if (text.StartsWith("!pulse") || text.StartsWith("!bg"))
            // Special color lines get their own parser. 🗿
            return ParseColorEvent(text);

        var sound_name_match = SoundNameRegex().Match(text);
        var sound = sound_name_match.Success ? sound_name_match.Value.Trim() : string.Empty;

        var value_match = ValueRegex().Match(text);
        var value = value_match.Success ? double.Parse(value_match.Value[1..], CultureInfo) : 0;

        var value_scale_match = ValueScaleRegex().Match(text);
        var scale = ValueScale.None;
        if (value_scale_match.Success)
        {
            var value_scale_string = value_scale_match.Value;
            scale = value_scale_string[^1] switch
            {
                'x' => ValueScale.Times,
                '+' => ValueScale.Add,
                '/' => ValueScale.Divide,
                _ => ValueScale.None
            };
        }

        var loop_times_match = LoopTimesRegex().Match(text);
        var loop_times = loop_times_match.Success ? float.Parse(loop_times_match.Value[1..], CultureInfo) : 1;

        var volume_match = VolumeRegex().Match(text);
        double? event_volume = volume_match.Success ? double.Parse(volume_match.Value[1..], CultureInfo) : null;

        var pan_match = PanRegex().Match(text);
        var pan = pan_match.Success ? float.Parse(pan_match.Value[1..], CultureInfo) : 0f;

        switch (sound)
        {
            case "!loopmany" or "!loop" or "!stop" or "_pause":
                loop_times = (float)(value > 0 ? value : loop_times);
                break;
            case "#bookmark":
                return new BookmarkEvent
                {
                    Value = value
                };
        }

        if (pan == 0f)
        {
            var new_event = new NormalEvent
            {
                Value = value,
                SoundEvent = string.Intern(sound),
                PlayTimes = loop_times,
                OriginalLoop = loop_times,
                ValueScale = scale,
                Volume = event_volume
            };

            return new_event;
        }
        else
        {
            var isNewFormat = Math.Abs(pan) > 1;
            
            var new_event = new PannedEvent
            {
                Pan = isNewFormat ? pan / 100f : pan,
                Value = value,
                SoundEvent = string.Intern(sound),
                PlayTimes = loop_times,
                OriginalLoop = loop_times,
                ValueScale = scale,
                Volume = event_volume,
                IsStandardImplementation = isNewFormat
            };

            return new_event;
        }
    }

    public static NormalEvent ParseColorEvent(string text)
    {
        var split_for_value = text.Split('@');
        var action = split_for_value[0];
        double value;

        if (split_for_value.Length < 2)
            throw new Exception(
                "A color event doesn't have a proper format. The format must be: \'!event@#rgb_or_num, number\'");

        var important_text = split_for_value[1];
        var split_data = important_text.Split(',');

        if (split_data.Length < 2)
            throw new Exception(
                "A color event doesn't have a proper format. The format must be: \'!event@#rgb_or_num, number\'");

        if (action is "!bg")
        {
            // Yes I created a custom encoding format for a RGB color and a decimal number. sue me
            var hex = split_data[0];
            byte r, g, b, a = 255;
            try
            {
                r = Convert.ToByte(hex[1..3], 16);
                g = Convert.ToByte(hex[3..5], 16);
                b = Convert.ToByte(hex[5..7], 16);
                if (hex.Length > 7)
                    a = Convert.ToByte(hex[7..9], 16);
            }
            catch (Exception e)
            {
                throw new Exception($"Parsing RGB colors in \'!bg\' action failed: \'{e}\'");
            }

            if (!double.TryParse(split_data[1], NumberStyles.Any, CultureInfo, out var parsed_fade_time))
                throw new Exception("Unable to parse fade time.");

            // Prepare fade time for encode
            var fade_time = TruncateToThreeDecimals(parsed_fade_time);

            // Make the number take up only 21 bits.
            var encoded_fade_time = (long)Math.Clamp(fade_time * 1000, 0, 128000);

            // Encode the value.
            var value_holder = (encoded_fade_time << 32) | (uint)(a << 24) | (uint)(b << 16) | (uint)(g << 8) | r;
            value = value_holder;
        }
        else // Action is pulse.
        {
            var count = split_data[0];
            if (!double.TryParse(count, NumberStyles.Any, CultureInfo, out var pulse_times))
                throw new Exception("Unable to parse \'!pulse\' action pulses.");

            var frequency = split_data[1];
            if (!double.TryParse(frequency, NumberStyles.Any, CultureInfo, out var repeats))
                throw new Exception("Unable to parse \'!pulse\' action pulses.");

            // No need to store decimal places as the site ignores them anyways.
            var r_byte = (byte)repeats;
            var p_short = (short)pulse_times;

            value = (p_short << 8) | r_byte;
        }

        var new_event = new NormalEvent
        {
            Value = value,
            SoundEvent = action,
            PlayTimes = 1,
            OriginalLoop = 1,
            ValueScale = ValueScale.None,
            Volume = null
        };
        return new_event;
    }

    private static double TruncateToThreeDecimals(double d)
    {
        return Math.Truncate(d * 1000) / 1000;
    }

    [GeneratedRegex("^[^@%^=]*", RegexOptions.IgnoreCase | RegexOptions.Multiline, "en-US")]
    private static partial Regex SoundNameRegex();

    [GeneratedRegex("@[-0-9.]+")]
    private static partial Regex ValueRegex();

    [GeneratedRegex("@[-0-9.]+@[^@%^=]+")]
    private static partial Regex ValueScaleRegex();

    [GeneratedRegex("=[0-9]+")]
    private static partial Regex LoopTimesRegex();

    [GeneratedRegex("%[-0-9.]+")]
    private static partial Regex VolumeRegex();

    [GeneratedRegex(@"\^[-0-9.]+")]
    private static partial Regex PanRegex();

    [GeneratedRegex(@"^#(?<name>[^\s(]+)\((?<value>[^)]+)\)")]
    private static partial Regex DefineRegex();

    [GeneratedRegex(@"^#icut\((?<events>[^)]+)\)")]
    private static partial Regex ICutRegex();

    [GeneratedRegex(@"^#bookmark\((?<index>[^)]+)\)")]
    private static partial Regex BookmarkRegex();
}