using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;

namespace ThirtyDollarConverter.Editor;

/// <summary>Which shape a dropped TDW sequence is converted into.</summary>
public enum ImportMode
{
    Track,
    Project
}

/// <summary>
///     Non-fatal issues found while importing: events the project model can't represent,
///     note positions that needed rounding to fit a subdivision grid, and sounds that
///     aren't in the current sample set.
/// </summary>
public sealed record ImportWarnings(
    IReadOnlyDictionary<string, int> IgnoredEvents,
    int QuantizedNotes,
    IReadOnlyList<string> UnknownSounds)
{
    public bool IsEmpty => IgnoredEvents.Count == 0 && QuantizedNotes == 0 && UnknownSounds.Count == 0;
}

/// <summary>
///     Track/Instruments/Placement are filled by <see cref="SequenceImporter.AddAsTrack" />
///     so <see cref="EditorScene.EditorState" /> can build its undo entry from them; they
///     stay null for <see cref="SequenceImporter.ToProject" />, which builds a whole project
///     instead (returned via its own out parameter).
/// </summary>
public sealed record ImportResult(ProjectTrack? Track, IReadOnlyList<Instrument> Instruments,
    TrackPlacement? Placement, ImportWarnings Warnings);

/// <summary>
///     Converts a parsed TDW <see cref="Sequence" /> into the editor's project model —
///     the inverse of <see cref="SequenceBuilder" />. Lives beside it so the two directions
///     of the mapping share one assembly and one test suite.
///     The conversion never mutates the input <see cref="Sequence" /> or (until it has fully
///     succeeded) the target project: the event walk and the region/segment math below are
///     pure, and only once they've completed does either public method touch a
///     <see cref="ThirtyDollarProject" /> — a walk that throws (malformed input, a runaway
///     loop/jump) can therefore never leave a half-imported track behind.
/// </summary>
public static class SequenceImporter
{
    /// <summary>Hard ceiling on walked events, guarding against a hostile or malformed
    /// loop/jump combination unrolling forever — a dropped file is a trust boundary.</summary>
    private const int MaxWalkedEvents = 1_000_000;

    private static readonly string[] JumpUntriggers = ["!loop", "!loopmany", "!jump", "!target"];
    private static readonly string[] LoopUntriggers = ["!loopmany", "!loop"];
    private static readonly string[] LoopmanyUntriggers = ["!loopmany"];

    /// <summary>Adds the sequence as one new track (+ its instruments + one placement) to an existing project.</summary>
    public static ImportResult AddAsTrack(ThirtyDollarProject project, Sequence sequence, string name,
        IReadOnlySet<string>? knownSounds)
    {
        var (walk, plans, warnings) = Prepare(sequence, knownSounds);

        var prefix = UniqueName(name, ExistingNames(project));
        var instruments = BuildInstruments(project, prefix, walk.SoundOrder);
        var segments = BuildTrackSegments(plans, instruments);

        var track = project.NewTrack();
        track.Name = prefix;
        PopulateTrack(track, segments);

        var channel = project.Placements.Count == 0 ? 0 : project.Placements.Max(p => p.Channel) + 1;
        var placement = project.Place(track, channel, 0);

        return new ImportResult(track, instruments.Values.ToArray(), placement, warnings);
    }

    /// <summary>Builds a whole new project: one track per distinct sound, sharing the same segment layout.</summary>
    public static ImportResult ToProject(Sequence sequence, string name, IReadOnlySet<string>? knownSounds,
        out ThirtyDollarProject project)
    {
        var (walk, plans, warnings) = Prepare(sequence, knownSounds);

        project = new ThirtyDollarProject { Info = { Name = name } };
        project.RootTiming.BPM = (float)walk.Regions[0].Speed;

        var prefix = UniqueName(name, ExistingNames(project));
        var instruments = BuildInstruments(project, prefix, walk.SoundOrder);
        var masterSegments = BuildTrackSegments(plans, instruments);

        var channel = 0;
        foreach (var sound in walk.SoundOrder)
        {
            var instrument = instruments[sound];
            var track = project.NewTrack();
            track.Name = $"{prefix} - {DisplayName(sound)}";

            var perSoundSegments = masterSegments.Select(segment =>
            {
                var copy = segment.Duplicate();
                copy.Notes.RemoveAll(note => note.Instrument != instrument);
                return copy;
            }).ToList();
            PopulateTrack(track, perSoundSegments);
            project.Place(track, channel++, 0);
        }

        return new ImportResult(null, instruments.Values.ToArray(), null, warnings);
    }

    private static (WalkData Walk, List<SegmentPlan> Plans, ImportWarnings Warnings) Prepare(Sequence sequence,
        IReadOnlySet<string>? knownSounds)
    {
        var walk = Walk(sequence, knownSounds);
        var plans = BuildSegmentPlans(walk.Regions, walk.Notes);
        var warnings = new ImportWarnings(walk.IgnoredEvents, plans.Sum(p => p.QuantizedNotes), walk.UnknownSounds);
        return (walk, plans, warnings);
    }

    private static IEnumerable<string> ExistingNames(ThirtyDollarProject project)
    {
        return project.Tracks.Select(t => t.Name).Concat(project.Instruments.Select(i => i.Name));
    }

    private static string DisplayName(string sound)
    {
        return sound.Replace('_', ' ');
    }

    /// <summary>First free "prefix", "prefix (2)", "prefix (3)", … whose name and
    /// "prefix - " namespace don't collide with any existing track/instrument name.</summary>
    private static string UniqueName(string prefix, IEnumerable<string> existingNames)
    {
        var names = existingNames.ToArray();
        if (!Collides(prefix)) return prefix;

        for (var n = 2;; n++)
        {
            var candidate = $"{prefix} ({n})";
            if (!Collides(candidate)) return candidate;
        }

        bool Collides(string candidate)
        {
            return names.Any(existing => existing == candidate || existing.StartsWith(candidate + " - "));
        }
    }

    // ---- Phase A: event walk (fractional step space, mirroring PlacementCalculator's control flow) ----

    private readonly record struct WalkedNote(int Region, double Step, string Sound, double Value, double? Volume,
        float Pan, double Offset, bool IsCut = false);

    private sealed record WalkData(List<(double Speed, double Length)> Regions, List<WalkedNote> Notes,
        List<string> SoundOrder, IReadOnlyDictionary<string, int> IgnoredEvents, IReadOnlyList<string> UnknownSounds);

    private static WalkData Walk(Sequence sequence, IReadOnlySet<string>? knownSounds)
    {
        var events = sequence.Copy().Events;
        var regions = new List<(double Speed, double Length)>();
        var notes = new List<WalkedNote>();
        var soundOrder = new List<string>();
        var seenSounds = new HashSet<string>();
        var ignoredEvents = new Dictionary<string, int>();
        var unknownSounds = new HashSet<string>();
        var seenCuts = new HashSet<(int Region, double Step, string Sound)>();

        var speed = 300d;
        var position = 0d;
        var globalVolume = 100d;
        var transpose = 0d;
        var loopTarget = 0;
        var regionIndex = 0;

        var index = 0;
        var walked = 0;
        while (index < events.Length)
        {
            if (++walked > MaxWalkedEvents)
                throw new InvalidOperationException(
                    "This sequence is too large or contains a runaway loop/jump to import safely.");

            var ev = events[index];
            var isAction = (ev.SoundEvent?.StartsWith('!') ?? true) || ev is ICustomActionEvent;

            if (!isAction)
            {
                var nextName = index + 1 < events.Length ? events[index + 1].SoundEvent : null;
                var advance = nextName is not "!combine";

                if (ev.SoundEvent is not "_pause")
                {
                    var sound = ev.SoundEvent!;
                    if (knownSounds is null || knownSounds.Contains(sound))
                    {
                        if (seenSounds.Add(sound)) soundOrder.Add(sound);
                        var pan = (ev as ExtendedEvent)?.Pan ?? 0f;
                        var offset = (ev as ExtendedEvent)?.OffsetInSeconds ?? 0d;
                        var baked = globalVolume == 100 ? ev.Volume : (ev.Volume ?? 100) * globalVolume / 100;
                        notes.Add(new WalkedNote(regionIndex, position, sound, ev.Value + transpose, baked, pan,
                            offset));
                    }
                    else
                    {
                        unknownSounds.Add(sound);
                    }
                }

                if (advance) position += 1;
                index++;
                continue;
            }

            switch (ev.SoundEvent)
            {
                case "!speed":
                {
                    var newSpeed = Scale(speed, ev);
                    if (!SequenceBuilder.SameSpeed(speed, newSpeed))
                    {
                        if (position > 1e-9) regions.Add((speed, position));
                        speed = newSpeed;
                        position = 0;
                        regionIndex = regions.Count;
                    }

                    break;
                }
                case "!volume":
                {
                    globalVolume = Math.Max(0, Scale(globalVolume, ev));
                    break;
                }
                case "!transpose":
                {
                    transpose = Scale(transpose, ev);
                    break;
                }
                case "!stop":
                    position += ev.Value;
                    break;
                case "!cut" when ev is IndividualCutEvent:
                case "#icut":
                {
                    var individualCut = (IndividualCutEvent)ev;
                    foreach (var sound in individualCut.CutSounds)
                    {
                        if (knownSounds is not null && !knownSounds.Contains(sound))
                        {
                            unknownSounds.Add(sound);
                            continue;
                        }

                        // Idempotent: repeating the same sound's cut at the same position
                        // (nothing else advancing position between them) collapses to one.
                        if (!seenCuts.Add((regionIndex, position, sound))) continue;

                        if (seenSounds.Add(sound)) soundOrder.Add(sound);
                        notes.Add(new WalkedNote(regionIndex, position, sound, 0, null, 0, 0, true));
                    }

                    break;
                }
                case "!cut": // bare global cut - the model has no way to represent it (see !cut@sound above)
                    ignoredEvents["!cut"] = ignoredEvents.GetValueOrDefault("!cut") + 1;
                    break;
                case "!looptarget":
                    loopTarget = index;
                    break;
                case "!loopmany":
                    if (ev.WorkingValue > 0)
                    {
                        ev.WorkingValue--;
                        index = loopTarget;
                        Untrigger(events, index, LoopmanyUntriggers);
                        continue;
                    }

                    break;
                case "!loop":
                    if (!ev.Triggered)
                    {
                        ev.Triggered = true;
                        index = loopTarget;
                        Untrigger(events, index, LoopUntriggers);
                        continue;
                    }

                    break;
                case "!jump":
                    if (!ev.Triggered)
                    {
                        ev.Triggered = true;
                        var target = Array.Find(events, e =>
                            e.SoundEvent == "!target" && Math.Abs(e.Value - ev.Value) < 0.001 && !e.Triggered);
                        if (target != null)
                        {
                            index = Array.IndexOf(events, target);
                            Untrigger(events, index, JumpUntriggers);
                            continue;
                        }
                    }

                    break;
                case "!combine":
                case "!target":
                case null or "":
                    break;
                default:
                    ignoredEvents[ev.SoundEvent!] = ignoredEvents.GetValueOrDefault(ev.SoundEvent!) + 1;
                    break;
            }

            index++;
        }

        if (position > 1e-9 || regions.Count == 0) regions.Add((speed, position));

        if (notes.Count == 0)
            throw new InvalidOperationException("No sounds found in this sequence.");

        return new WalkData(regions, notes, soundOrder, ignoredEvents, unknownSounds.Order().ToArray());
    }

    private static double Scale(double current, BaseEvent ev)
    {
        return ev.ValueScale switch
        {
            ValueScale.Divide => current / ev.Value,
            ValueScale.Times => current * ev.Value,
            ValueScale.Add => current + ev.Value,
            _ => ev.Value
        };
    }

    /// <summary>Ported from PlacementCalculator.Untrigger: re-arms loop/jump triggers from
    /// <paramref name="index" /> onward (except the given event names) so nested loops can fire again.</summary>
    private static void Untrigger(BaseEvent[] events, int index, string[] except)
    {
        if (index == 0) index++;
        for (var i = index - 1; i < events.Length; i++)
        {
            var current = events[i];
            if (except.Contains(current.SoundEvent)) continue;
            current.Triggered = false;
            current.WorkingValue = current.Value;
        }
    }

    // ---- Phase B: regions -> segments (each speed change becomes its own segment) ----

    private sealed record SegmentPlan(int Numerator, int Bars, float BPM, List<(int Step, WalkedNote Note)> Notes,
        int QuantizedNotes);

    private static List<SegmentPlan> BuildSegmentPlans(List<(double Speed, double Length)> regions,
        List<WalkedNote> notes)
    {
        var byRegion = notes.ToLookup(n => n.Region);
        var plans = new List<SegmentPlan>(regions.Count);

        for (var r = 0; r < regions.Count; r++)
        {
            var (speed, length) = regions[r];
            var regionNotes = byRegion[r].ToArray();

            // The k that minimizes misalignment, not "first exact fit, else the 64 cap":
            // a few unrepresentable fractional positions (swing/humanization stops with
            // an odd denominator) shouldn't force every OTHER already-whole note onto an
            // unnecessarily fine subdivision. A finer grid doesn't just fail to help those
            // outliers (64 is no likelier a multiple of their true denominator than 1 is)
            // — it also makes real playback's own per-step integer sample truncation lose
            // more total precision across the whole region, compounding into audible drift
            // for every note, not just the outliers. A region-length mismatch is weighted
            // far above any single note's, since it drifts every later region too.
            var k = 1;
            var bestScore = int.MaxValue;
            for (var candidate = 1; candidate <= SequenceBuilder.MaxSpeedMultiplier; candidate++)
            {
                var noteFailures = regionNotes.Count(n => !IsWholeStep(n.Step * candidate));
                var lengthFailure = IsWholeStep(length * candidate) ? 0 : 1;
                var score = lengthFailure * (regionNotes.Length + 1) + noteFailures;
                if (score >= bestScore) continue;

                bestScore = score;
                k = candidate;
                if (score == 0) break;
            }

            var quantized = 0;
            var stepped = regionNotes.Select(n =>
            {
                var raw = n.Step * k;
                if (!IsWholeStep(raw)) quantized++;
                return ((int)Math.Round(raw), n);
            }).ToList();

            var lengthSteps = (int)Math.Round(length * k);
            int numerator, bars;
            float bpm;
            if (k == 1 && lengthSteps % 4 == 0)
            {
                numerator = 4;
                bars = lengthSteps / 4;
                bpm = (float)speed;
            }
            else
            {
                numerator = lengthSteps;
                bars = 1;
                bpm = (float)(speed * k);
            }

            plans.Add(new SegmentPlan(numerator, bars, bpm, stepped, quantized));
        }

        return plans;
    }

    private static bool IsWholeStep(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 1e-6;
    }

    // ---- Phase C: instruments and notes ----

    private static Dictionary<string, Instrument> BuildInstruments(ThirtyDollarProject project, string prefix,
        List<string> soundOrder)
    {
        var instruments = new Dictionary<string, Instrument>();
        foreach (var sound in soundOrder)
        {
            var instrument = project.NewInstrument($"{prefix} - {DisplayName(sound)}");
            instrument.Sounds.Add(sound);
            instruments[sound] = instrument;
        }

        return instruments;
    }

    private static List<TrackSegment> BuildTrackSegments(List<SegmentPlan> plans,
        Dictionary<string, Instrument> instruments)
    {
        var segments = new List<TrackSegment>(plans.Count);
        foreach (var plan in plans)
        {
            var segment = new TrackSegment
            {
                Numerator = plan.Numerator, Denominator = 4, StepsPerBeat = 1, Bars = plan.Bars, BPM = plan.BPM
            };
            foreach (var (step, note) in plan.Notes)
                segment.Notes.Add(new Note
                {
                    Step = step,
                    Instrument = instruments[note.Sound],
                    Value = note.Value,
                    Volume = note.Volume,
                    Pan = note.Pan,
                    Offset = note.Offset,
                    IsCut = note.IsCut
                });
            segments.Add(segment);
        }

        return segments;
    }

    // ---- Phase D: assembly ----

    /// <summary>The first segment overwrites the track's default one (NewTrack starts with
    /// one) instead of appending, or every import would gain a phantom 4/4 bar.</summary>
    private static void PopulateTrack(ProjectTrack track, IReadOnlyList<TrackSegment> segments)
    {
        for (var i = 0; i < segments.Count; i++)
        {
            var target = i == 0 ? track.Segments[0] : track.NewSegment();
            var source = segments[i];
            target.Numerator = source.Numerator;
            target.Denominator = source.Denominator;
            target.StepsPerBeat = source.StepsPerBeat;
            target.Bars = source.Bars;
            target.BPM = source.BPM;
            target.Notes.AddRange(source.Notes);
        }
    }
}
