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
public sealed record ImportResult(
    ProjectTrack? Track,
    IReadOnlyList<Instrument> Instruments,
    TrackPlacement? Placement,
    ImportWarnings Warnings);

/// <summary>
///     Converts a parsed TDW <see cref="Sequence" /> into the editor's project model -
///     the inverse of <see cref="SequenceBuilder" />. Lives beside it so the two directions
///     of the mapping share one assembly and one test suite.
///     The conversion never mutates the input <see cref="Sequence" /> or (until it has fully
///     succeeded) the target project: the event walk and the region/segment math below are
///     pure, and only once they've completed does either public method touch a
///     <see cref="ThirtyDollarProject" /> - a walk that throws (malformed input, a runaway
///     loop/jump) can therefore never leave a half-imported track behind.
/// </summary>
public static class SequenceImporter
{
    /// <summary>
    ///     Hard ceiling on walked events, guarding against a hostile or malformed
    ///     loop/jump combination unrolling forever - a dropped file is a trust boundary.
    /// </summary>
    private const int MaxWalkedEvents = 1_000_000;

    /// <summary>
    ///     How fine a merged segment's grid may get, relative to its slowest region,
    ///     before a speed change is treated as a real tempo change and split off instead.
    /// </summary>
    private const int MaxMergedSubdivision = 4;

    /// <summary>
    ///     Total budget for how far a segment's grid may run above its slowest region's speed.
    ///     Merging and off-grid note fitting both spend from it, and they have to share one
    ///     budget because they multiply: a group merged 8x that then fits 1/10th-step swing
    ///     lands on an 80x grid, and since StepsPerBeat can't exceed
    ///     <see cref="SequenceBuilder.MaxSpeedMultiplier" />, the overflow has nowhere to go but
    ///     the BPM - which is how a plain 145 BPM track came out as 181.25 BPM at 64 steps/beat.
    ///     Spending the rest of the budget on rounding a few swung notes onto the grid (counted
    ///     in <see cref="ImportWarnings.QuantizedNotes" />) beats keeping every note exact at the
    ///     cost of a tempo nobody wrote.
    /// </summary>
    private const int MaxGridSubdivision = 32;

    // ponytail: the two weights below are feel, sized against the penalties above - tune
    // against the ~/tdw corpus if trailing bars / padding look too eager or too timid.
    // Padding is scored as a fraction of the group's length, so a few steps of silence at
    // the end of a long track is nearly free while inflating a two-note tail is not.
    private const double RemainderBarPenalty = 1.5;
    private const double TempoBreakPenalty = 4.0;
    private const double PadPenalty = 8.0;

    private static readonly string[] JumpUntriggers = ["!loop", "!loopmany", "!jump", "!target"];
    private static readonly string[] LoopUntriggers = ["!loopmany", "!loop"];
    private static readonly string[] LoopmanyUntriggers = ["!loopmany"];

    /// <summary>
    ///     Time signature numerators worth using, best first. A note grid almost never
    ///     implies a numerator on its own, so an exotic one is nearly always the symptom of a
    ///     length that doesn't divide into bars - which a short trailing bar reports honestly
    ///     and 59 bars of 5/4 does not.
    /// </summary>
    private static readonly int[] Numerators = [4, 3, 2, 6, 8, 5, 7, 9];

    /// <summary>
    ///     Sub-grid multipliers worth considering: halves, thirds and their products.
    ///     A "!stop@0.05" would land exactly on a 20x grid, but 20 steps to a beat is not a grid
    ///     anyone can edit against - rounding those few notes onto a musical grid (and reporting
    ///     it in <see cref="ImportWarnings.QuantizedNotes" />) is the better trade.
    /// </summary>
    private static readonly int[] SubGrids = [1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64];

    /// <summary>Adds the sequence as one new track (+ its instruments + one placement) to an existing project.</summary>
    public static ImportResult AddAsTrack(ThirtyDollarProject project, Sequence sequence, string name,
        IReadOnlyDictionary<string, Sound>? soundMap)
    {
        var (walk, plans, warnings) = Prepare(sequence, soundMap);

        var track = project.NewTrack();
        track.Name = UniqueName($"{name} - imported", ExistingNames(project));

        var instruments = BuildInstruments(project, walk.SoundOrder);
        var segments = BuildTrackSegments(plans, instruments);
        PopulateTrack(track, segments);

        var channel = project.Placements.Count == 0 ? 0 : project.Placements.Max(p => p.Channel) + 1;
        var placement = project.Place(track, channel, 0);

        return new ImportResult(track, instruments.Values.ToArray(), placement, warnings);
    }

    /// <summary>Builds a whole new project: one track per distinct sound, sharing the same segment layout.</summary>
    public static ImportResult ToProject(Sequence sequence, string name, IReadOnlyDictionary<string, Sound>? soundMap,
        out ThirtyDollarProject project)
    {
        var (walk, plans, warnings) = Prepare(sequence, soundMap);

        project = new ThirtyDollarProject { Info = { Name = name } };
        project.RootTiming.BPM = plans[0].BPM;

        var instruments = BuildInstruments(project, walk.SoundOrder);
        var masterSegments = BuildTrackSegments(plans, instruments);

        var channel = 0;
        foreach (var sound in walk.SoundOrder)
        {
            var instrument = instruments[sound];
            var track = project.NewTrack();
            track.Name = instrument.Name;

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
        IReadOnlyDictionary<string, Sound>? soundMap)
    {
        var walk = Walk(sequence, soundMap);
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

    /// <summary>
    ///     First free "name", "name (2)", "name (3)", … not already used by any
    ///     existing track or instrument.
    /// </summary>
    private static string UniqueName(string name, IEnumerable<string> existingNames)
    {
        var names = existingNames.ToHashSet();
        if (!names.Contains(name)) return name;

        for (var n = 2;; n++)
        {
            var candidate = $"{name} ({n})";
            if (!names.Contains(candidate)) return candidate;
        }
    }

    /// <summary>
    ///     A sequence names a sound however it was saved - a TDW emoji ("🍕") just as
    ///     often as an ID ("pizza"). Everything downstream (instrument names, and the labels
    ///     drawing them) wants the ID, so names are canonicalised here, at the one point where
    ///     a sequence enters the project model. Null means the sample set doesn't know the
    ///     sound; a null map (no samples loaded) accepts every name as-is.
    /// </summary>
    private static string? CanonicalId(string sound, IReadOnlyDictionary<string, Sound>? soundMap)
    {
        if (soundMap is null) return sound;
        return soundMap.TryGetValue(sound, out var match) ? match.Id : null;
    }

    private static WalkData Walk(Sequence sequence, IReadOnlyDictionary<string, Sound>? soundMap)
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
                    if (CanonicalId(ev.SoundEvent!, soundMap) is { } sound)
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
                        unknownSounds.Add(ev.SoundEvent!);
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
                    foreach (var cutSound in individualCut.CutSounds)
                    {
                        if (CanonicalId(cutSound, soundMap) is not { } sound)
                        {
                            unknownSounds.Add(cutSound);
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
                case "!cut": // bare global cut - only sounds already introduced could be playing
                {
                    foreach (var sound in soundOrder)
                    {
                        if (!seenCuts.Add((regionIndex, position, sound))) continue;
                        notes.Add(new WalkedNote(regionIndex, position, sound, 0, null, 0, 0, true));
                    }

                    break;
                }
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

    /// <summary>
    ///     Ported from PlacementCalculator.Untrigger: re-arms loop/jump triggers from
    ///     <paramref name="index" /> onward (except the given event names) so nested loops can fire again.
    /// </summary>
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

    private static List<SegmentPlan> BuildSegmentPlans(List<(double Speed, double Length)> regions,
        List<WalkedNote> notes)
    {
        var byRegion = notes.ToLookup(n => n.Region);

        // Absolute start of every region, in minutes: a merged segment has to place notes
        // from several regions on one shared grid.
        var starts = new double[regions.Count + 1];
        for (var r = 0; r < regions.Count; r++)
            starts[r + 1] = starts[r] + regions[r].Length / regions[r].Speed;

        var plans = new List<SegmentPlan>();
        for (var first = 0; first < regions.Count;)
        {
            var (last, rate, slowest) = GroupRegions(regions, first);

            var placed = new List<(double Step, WalkedNote Note)>();
            for (var r = first; r <= last; r++)
                foreach (var note in byRegion[r])
                    placed.Add(((starts[r] - starts[first] + note.Step / regions[r].Speed) * rate, note));

            var length = (starts[last + 1] - starts[first]) * rate;
            var k = BestSubGrid(placed, length, Math.Max(1, (int)(MaxGridSubdivision * slowest / rate)));

            var quantized = 0;
            var stepped = placed.Select(p =>
            {
                var scaled = p.Step * k;
                if (!IsWholeStep(scaled)) quantized++;
                return ((int)Math.Round(scaled), p.Note);
            }).OrderBy(p => p.Item1).ToList();

            // A group can round to zero steps only when it is a lone "!combine"d stack at
            // step 0; give it one step so its notes still land inside the grid.
            var lengthSteps = Math.Max((int)Math.Round(length * k), stepped.Count > 0 ? 1 : 0);
            AddShape(plans, rate * k, lengthSteps, stepped, quantized, last == regions.Count - 1);

            first = last + 1;
        }

        return plans;
    }

    private static bool IsWholeStep(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 1e-6;
    }

    /// <summary>
    ///     How far the segment starting at <paramref name="first" /> reaches, and the grid rate
    ///     it runs at. Consecutive regions merge while one of the two speeds is a whole multiple
    ///     of the other: "!speed@2@x" is a subdivision change, not a tempo change, and cutting a
    ///     segment at every one of them is what turned a steady 177 BPM track into thirty
    ///     segments of 19 steps per beat - a region left holding an awkward number of steps has
    ///     no musical subdivision that divides it. A ratio that isn't whole (150 -> 90) is a real
    ///     tempo change and still starts a new segment.
    /// </summary>
    private static (int Last, double Rate, double Slowest) GroupRegions(
        List<(double Speed, double Length)> regions, int first)
    {
        var rate = regions[first].Speed;
        var slowest = rate;
        var last = first;

        while (last + 1 < regions.Count)
        {
            var speed = regions[last + 1].Speed;
            var merged = Math.Max(rate, speed);
            if (!IsWholeStep(merged / rate) || !IsWholeStep(merged / speed)) break;
            if (merged / Math.Min(slowest, speed) > MaxMergedSubdivision) break;

            rate = merged;
            slowest = Math.Min(slowest, speed);
            last++;
        }

        return (last, rate, slowest);
    }

    /// <summary>
    ///     The subdivision of the group's grid that leaves the fewest positions off it. Not
    ///     "first exact fit, else the 64 cap": a few unrepresentable fractional positions
    ///     (swing/humanization stops with an odd denominator) shouldn't force every OTHER
    ///     already-whole note onto an unnecessarily fine subdivision. A finer grid doesn't just
    ///     fail to help those outliers (64 is no likelier a multiple of their true denominator
    ///     than 1 is) - it also makes real playback's own per-step integer sample truncation lose
    ///     more total precision across the whole group, compounding into audible drift for every
    ///     note, not just the outliers. A length mismatch is weighted far above any single note's,
    ///     since it drifts every later group too.
    /// </summary>
    private static int BestSubGrid(List<(double Step, WalkedNote Note)> notes, double length, int maxK)
    {
        var best = 1;
        var bestScore = int.MaxValue;

        foreach (var k in SubGrids)
        {
            if (k > maxK) break;

            var score = (IsWholeStep(length * k) ? 0 : notes.Count + 1)
                        + notes.Count(n => !IsWholeStep(n.Step * k));
            if (score >= bestScore) continue;

            bestScore = score;
            best = k;
            if (score == 0) break;
        }

        return best;
    }

    /// <summary>
    ///     Appends the segment(s) covering one group: a run of whole bars in the best-scoring
    ///     time signature, plus a short trailing bar holding whatever doesn't fill one. Every
    ///     (Spb, Numerator) satisfying <c>BPM * Spb == rate</c> is timing-identical, so the choice
    ///     is only about which one a human would have written: penalize an unmusical subdivision,
    ///     an inhuman BPM and an odd time signature. Only the final group
    ///     (<paramref name="isFinal" />) may instead round up with trailing silence (never emitted
    ///     by <see cref="SequenceBuilder" />); padding a middle group would shift every later one.
    ///     The trailing bar picks its own shape rather than inheriting the run's, so a group whose
    ///     length is prime (31 steps) doesn't collapse the whole run onto the only subdivision
    ///     that divides it - which is how a 192 BPM track produced 6144 BPM segments.
    /// </summary>
    private static void AddShape(List<SegmentPlan> plans, double rate, int lengthSteps,
        List<(int Step, WalkedNote Note)> notes, int quantized, bool isFinal)
    {
        if (lengthSteps <= 0)
        {
            plans.Add(new SegmentPlan(4, 0, 1, (float)rate, notes, quantized));
            return;
        }

        var best = (Spb: 1, Numerator: 4, Bars: 0, Score: double.MaxValue);
        for (var spb = 1; spb <= SequenceBuilder.MaxSpeedMultiplier; spb++)
        {
            var grid = SpbPenalty(spb) + BpmPenalty(rate / spb);

            foreach (var n in Numerators)
            {
                var bar = n * spb;
                var bars = lengthSteps / bar;
                if (bars == 0) continue;

                // A tail that can keep the run's subdivision is just a short bar; one that
                // can't has to state its own BPM too, which reads as a tempo change the
                // sequence never made. Padding is often the lesser evil, so price it higher.
                var shape = grid + NumeratorPenalty(n);
                var score = shape + (lengthSteps % bar == 0 ? 0 :
                    lengthSteps % spb == 0 ? RemainderBarPenalty : TempoBreakPenalty);
                if (score < best.Score) best = (spb, n, bars, score);

                if (!isFinal || lengthSteps % bar == 0) continue;
                var padScore = shape + ((bars + 1) * bar - lengthSteps) * PadPenalty / lengthSteps;
                if (padScore < best.Score) best = (spb, n, bars + 1, padScore);
            }
        }

        // Padding (final group only) makes the run longer than the group; every note still
        // falls inside it, and there is no leftover to place.
        var mainSteps = best.Bars * best.Numerator * best.Spb;
        if (best.Bars > 0)
            plans.Add(new SegmentPlan(best.Numerator, best.Bars, best.Spb, (float)(rate / best.Spb),
                notes.TakeWhile(p => p.Step < mainSteps).ToList(), quantized));

        var leftover = lengthSteps - mainSteps;
        if (leftover <= 0) return;

        var tailNotes = notes.SkipWhile(p => p.Step < mainSteps).ToList();

        // Trailing silence is never represented (SequenceBuilder drops it), so a final tail
        // holding no notes is a segment with nothing in it - don't invent one.
        if (isFinal && tailNotes.Count == 0) return;

        // The tail keeps the run's tempo whenever its length allows: a segment that changes
        // BPM purely because the bar before it ran out of steps is nonsense. It only picks its
        // own shape when the run's subdivision doesn't divide it.
        var (spb2, numerator) = leftover % best.Spb == 0
            ? (best.Spb, leftover / best.Spb)
            : BestBar(rate, leftover);

        plans.Add(new SegmentPlan(numerator, 1, spb2, (float)(rate / spb2),
            tailNotes.Select(p => (p.Step - mainSteps, p.Note)).ToList(),
            best.Bars > 0 ? 0 : quantized));
    }

    /// <summary>
    ///     The (StepsPerBeat, Numerator) for a single bar spanning exactly
    ///     <paramref name="steps" /> steps, scored the same way a full run is.
    /// </summary>
    private static (int Spb, int Numerator) BestBar(double rate, int steps)
    {
        var best = (Spb: 1, Numerator: steps, Score: double.MaxValue);

        for (var spb = 1; spb <= Math.Min(steps, SequenceBuilder.MaxSpeedMultiplier); spb++)
        {
            if (steps % spb != 0) continue;

            var score = SpbPenalty(spb) + BpmPenalty(rate / spb) + NumeratorPenalty(steps / spb);
            if (score < best.Score) best = (spb, steps / spb, score);
        }

        return (best.Spb, best.Numerator);
    }

    /// <summary>
    ///     Musical subdivisions first - halves and quarters of a beat, then triplets,
    ///     then whatever's left. A 5- or 19-step beat is a symptom, not a style.
    /// </summary>
    private static double SpbPenalty(int spb)
    {
        return spb switch
        {
            4 or 8 => 0,
            2 or 16 => 0.5,
            1 or 32 => 1.5,
            3 or 6 or 12 or 24 or 48 => 2,
            64 => 3,
            _ => 20
        };
    }

    private static double NumeratorPenalty(int n)
    {
        var rank = Array.IndexOf(Numerators, n);
        return rank < 0 ? 20 : rank * 0.75;
    }

    /// <summary>
    ///     Distance outside the tempo band real music lives in, in halvings/doublings. The band
    ///     has to be tight: every shape here is a doubling of some other shape, so a flat "60-300
    ///     is fine" scores 177 and 354 identically and then lets a tiebreak pick the wrong one.
    /// </summary>
    private static double BpmPenalty(double bpm)
    {
        if (bpm is >= 70 and <= 190) return 0;
        return 3 * Math.Abs(Math.Log2(bpm / (bpm < 70 ? 70 : 190)));
    }

    // ---- Phase C: instruments and notes ----

    private static Dictionary<string, Instrument> BuildInstruments(ThirtyDollarProject project,
        List<string> soundOrder)
    {
        var instruments = new Dictionary<string, Instrument>();
        foreach (var sound in soundOrder)
        {
            var name = UniqueName($"{DisplayName(sound)} - imported", ExistingNames(project));
            var instrument = project.NewInstrument(name);
            instrument.AddSound(sound);
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
                Numerator = plan.Numerator,
                Denominator = 4,
                StepsPerBeat = plan.Spb,
                Bars = plan.Bars,
                BPM = plan.BPM
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

    /// <summary>
    ///     The first segment overwrites the track's default one (NewTrack starts with
    ///     one) instead of appending, or every import would gain a phantom 4/4 bar.
    /// </summary>
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

    // ---- Phase A: event walk (fractional step space, mirroring PlacementCalculator's control flow) ----

    private readonly record struct WalkedNote(
        int Region,
        double Step,
        string Sound,
        double Value,
        double? Volume,
        float Pan,
        double Offset,
        bool IsCut = false);

    private sealed record WalkData(
        List<(double Speed, double Length)> Regions,
        List<WalkedNote> Notes,
        List<string> SoundOrder,
        IReadOnlyDictionary<string, int> IgnoredEvents,
        IReadOnlyList<string> UnknownSounds);

    // ---- Phase B: regions -> segments ----

    private sealed record SegmentPlan(
        int Numerator,
        int Bars,
        int Spb,
        float BPM,
        List<(int Step, WalkedNote Note)> Notes,
        int QuantizedNotes);
}