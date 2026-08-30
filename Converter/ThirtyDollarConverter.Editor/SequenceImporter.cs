using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace ThirtyDollarConverter.Editor;

/// <summary>Which shape a dropped TDW sequence is converted into.</summary>
public enum ImportMode
{
    /// <summary>One piano-roll track: the sequence fitted onto a bar/beat grid.</summary>
    Track,

    /// <summary>One faithful track: the sequence's own events, kept as they are.</summary>
    Faithful,

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
///     the inverse of <see cref="SequenceBuilder" />. Never mutates the input sequence, and
///     touches the target <see cref="ThirtyDollarProject" /> only once the conversion has
///     fully succeeded, so a failed import leaves no half-imported track behind.
/// </summary>
public static class SequenceImporter
{
    /// <summary>
    ///     How fine a merged segment's grid may get, relative to its slowest region,
    ///     before a speed change is treated as a real tempo change and split off instead.
    /// </summary>
    private const int MaxMergedSubdivision = 4;

    /// <summary>
    ///     Total budget for how far a segment's grid may run above its slowest region's speed.
    ///     Merging and off-grid note fitting share it because they multiply, and StepsPerBeat is
    ///     capped at <see cref="SequenceBuilder.MaxSpeedMultiplier" />, so overflow would land in
    ///     the BPM instead. Notes rounded onto the grid to stay inside the budget are counted in
    ///     <see cref="ImportWarnings.QuantizedNotes" />.
    /// </summary>
    private const int MaxGridSubdivision = 32;

    // ponytail: the two weights below are feel, sized against the penalties above - tune
    // against the ~/tdw corpus if trailing bars / padding look too eager or too timid.
    // Padding is scored as a fraction of the group's length, so a few steps of silence at
    // the end of a long track is nearly free while inflating a two-note tail is not.
    private const double RemainderBarPenalty = 1.5;
    private const double TempoBreakPenalty = 4.0;
    private const double PadPenalty = 8.0;

    /// <summary>
    ///     Time signature numerators worth using, best first. A note grid almost never implies
    ///     a numerator on its own, so an exotic one ranks last: it is nearly always the symptom
    ///     of a length that doesn't divide into bars, which a short trailing bar states better.
    /// </summary>
    private static readonly int[] Numerators = [4, 3, 2, 6, 8, 5, 7, 9];

    /// <summary>
    ///     Sub-grid multipliers worth considering: halves, thirds and their products. Only
    ///     these are offered, so the grid stays one a human can edit against; positions that
    ///     don't land on one are rounded and reported in
    ///     <see cref="ImportWarnings.QuantizedNotes" />.
    /// </summary>
    private static readonly int[] SubGrids = [1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64];

    /// <summary>Adds the sequence as one new track (+ its instruments + one placement) to an existing project.</summary>
    public static ImportResult AddAsTrack(ThirtyDollarProject project, Sequence sequence, string name,
        IReadOnlyDictionary<string, Sound>? soundMap)
    {
        var (walk, plans, warnings) = Prepare(sequence, soundMap);

        var track = project.NewTrack();
        track.Name = UniqueName($"{name} - imported", ExistingNames(project));

        var created = new List<Instrument>();
        var instruments = BuildInstruments(project, walk.SoundOrder, created);
        var segments = BuildTrackSegments(plans, instruments);
        PopulateTrack(track, segments);

        var channel = project.Placements.Count == 0 ? 0 : project.Placements.Max(p => p.Channel) + 1;
        var placement = project.Place(track, channel, 0);

        return new ImportResult(track, created, placement, warnings);
    }

    /// <summary>
    ///     Adds the sequence as one new faithful track: its events kept verbatim as items, with
    ///     no grid, no quantization and nothing ignored, so the only warning it can raise is a
    ///     sound the sample set doesn't know. A "!combine"-joined run collapses into one layered
    ///     instrument, reusing an existing instrument whose sounds match rather than adding a
    ///     duplicate.
    /// </summary>
    public static ImportResult AddAsFaithfulTrack(ThirtyDollarProject project, Sequence sequence, string name,
        IReadOnlyDictionary<string, Sound>? soundMap)
    {
        var events = sequence.Events;
        // Checked before anything is created, so a sequence this can't take leaves the
        // project untouched - the same all-or-nothing contract AddAsTrack has.
        if (events.Length == 0) throw new InvalidOperationException("No events found in this sequence.");

        var unknownSounds = new HashSet<string>();
        var created = new List<Instrument>();
        var items = new List<FaithfulItem>();

        for (var i = 0; i < events.Length; i++)
        {
            if (!IsSound(events[i]))
            {
                items.Add(new FaithfulItem { Action = events[i].Copy() });
                continue;
            }

            var run = new List<BaseEvent> { events[i] };
            while (i + 2 < events.Length && events[i + 1].SoundEvent == "!combine" && IsSound(events[i + 2]))
            {
                run.Add(events[i + 2]);
                i += 2;
            }

            // A run only becomes one layered instrument when its sounds agree on volume, pan
            // and offset - the three a Note carries once for the whole item. A run that doesn't
            // stays a slot each joined by "!combine" items, which is how the site draws them and
            // keeps per-sound "%volume" out of the instrument palette.
            if (!Uniform(run))
            {
                for (var s = 0; s < run.Count; s++)
                {
                    if (s > 0) items.Add(Combine());
                    items.Add(SoundItem(project, [run[s]], soundMap, unknownSounds, created) ?? Pause());
                }

                continue;
            }

            // A run this sample set knows nothing about still held its step, so it leaves a
            // "_pause" behind rather than pulling every later sound one step early.
            items.Add(SoundItem(project, run, soundMap, unknownSounds, created) ?? Pause());
        }

        var track = (FaithfulTrack)project.NewTrack(TrackKind.Faithful);
        track.Name = UniqueName($"{name} - imported", ExistingNames(project));
        track.Items.AddRange(items);

        var channel = project.Placements.Count == 0 ? 0 : project.Placements.Max(p => p.Channel) + 1;
        var placement = project.Place(track, channel, 0);

        return new ImportResult(track, created, placement,
            new ImportWarnings(new Dictionary<string, int>(), 0, [.. unknownSounds.Order()]));
    }

    /// <summary>
    ///     A playable sound, classified exactly as <see cref="SequenceWalker" /> does it.
    ///     "_pause" is silence, so it rides with the actions - which is also where the
    ///     faithful palette keeps it.
    /// </summary>
    private static bool IsSound(BaseEvent ev)
    {
        return ev.SoundEvent is { } sound && !sound.StartsWith('!') && sound != "_pause" &&
               ev is not ICustomActionEvent;
    }

    /// <summary>
    ///     One sound item from a "!combine"-joined run, or from a single sound. Null when the
    ///     sample set knows none of them.
    ///     The run's sounds agree on volume, pan and offset by the time they get here (see
    ///     <see cref="Uniform" /> and its caller), so the note carries those; only the pitch
    ///     interval between them lives on the instrument, relative to its first sound.
    /// </summary>
    private static FaithfulItem? SoundItem(ThirtyDollarProject project, List<BaseEvent> run,
        IReadOnlyDictionary<string, Sound>? soundMap, HashSet<string> unknownSounds, List<Instrument> created)
    {
        var sounds = new List<(string Id, BaseEvent Event)>();
        foreach (var ev in run)
        {
            if (CanonicalId(ev.SoundEvent!, soundMap) is { } id) sounds.Add((id, ev));
            else unknownSounds.Add(ev.SoundEvent!);
        }

        if (sounds.Count == 0) return null;

        var first = sounds[0].Event;
        var candidate = new Instrument();
        foreach (var (id, ev) in sounds) candidate.AddSound(id).Value = ev.Value - first.Value;

        var instrument = Adopt(project, candidate, sounds[0].Id, created);

        return new FaithfulItem
        {
            Note = new Note
            {
                Step = 0,
                Instrument = instrument,
                Value = first.Value,
                Volume = first.Volume,
                Pan = PanOf(first),
                Offset = OffsetOf(first)
            }
        };
    }

    /// <summary>Whether every sound of a run carries the same volume, pan and offset.</summary>
    private static bool Uniform(List<BaseEvent> run)
    {
        var first = run[0];
        return run.All(ev => Nullable.Equals(ev.Volume, first.Volume) &&
                             Math.Abs(PanOf(ev) - PanOf(first)) < 1e-6f &&
                             Math.Abs(OffsetOf(ev) - OffsetOf(first)) < 1e-9);
    }

    /// <summary>Layers the next slot onto this one, without advancing the step.</summary>
    private static FaithfulItem Combine()
    {
        return new FaithfulItem
        {
            Action = new NormalEvent { SoundEvent = "!combine", ValueScale = ValueScale.None }
        };
    }

    /// <summary>TDW's silent sound: one step, nothing played.</summary>
    private static FaithfulItem Pause()
    {
        return new FaithfulItem
        {
            Action = new NormalEvent { SoundEvent = "_pause", ValueScale = ValueScale.None }
        };
    }

    private static float PanOf(BaseEvent ev)
    {
        return (ev as ExtendedEvent)?.Pan ?? 0f;
    }

    private static double OffsetOf(BaseEvent ev)
    {
        return (ev as ExtendedEvent)?.OffsetInSeconds ?? 0d;
    }

    /// <summary>Whether two instruments play the same sounds with the same tuning, in the same order.</summary>
    private static bool SameSounds(Instrument a, Instrument b)
    {
        return a.Sounds.Count == b.Sounds.Count && a.Sounds.Zip(b.Sounds).All(pair =>
            pair.First.Sound == pair.Second.Sound &&
            Math.Abs(pair.First.Value - pair.Second.Value) < 1e-9 &&
            Nullable.Equals(pair.First.Volume, pair.Second.Volume) &&
            Math.Abs(pair.First.Pan - pair.Second.Pan) < 1e-6);
    }

    /// <summary>Builds a whole new project: one track per distinct sound, sharing the same segment layout.</summary>
    public static ImportResult ToProject(Sequence sequence, string name, IReadOnlyDictionary<string, Sound>? soundMap,
        out ThirtyDollarProject project)
    {
        var (walk, plans, warnings) = Prepare(sequence, soundMap);

        project = new ThirtyDollarProject
        {
            Info = { Name = name },
            RootTiming =
            {
                BPM = plans[0].BPM
            }
        };

        var created = new List<Instrument>();
        var instruments = BuildInstruments(project, walk.SoundOrder, created);
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

        return new ImportResult(null, created, null, warnings);
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
    ///     The sample set's ID for a sound named either by ID ("pizza") or by its TDW emoji
    ///     ("🍕"). Everything downstream wants the ID, so names are canonicalised here, at the
    ///     one point where a sequence enters the project model. Null means the sample set
    ///     doesn't know the sound; a null map (no samples loaded) accepts every name as-is.
    /// </summary>
    private static string? CanonicalId(string sound, IReadOnlyDictionary<string, Sound>? soundMap)
    {
        if (soundMap is null) return sound;
        return soundMap.TryGetValue(sound, out var match) ? match.Id : null;
    }

    private static WalkData Walk(Sequence sequence, IReadOnlyDictionary<string, Sound>? soundMap)
    {
        var walk = SequenceWalker.Walk(sequence.Events);
        if (walk.Truncated)
            throw new InvalidOperationException(
                "This sequence is too large or contains a runaway loop/jump to import safely.");

        var notes = new List<WalkedNote>();
        var soundOrder = new List<string>();
        var seenSounds = new HashSet<string>();
        var ignoredEvents = new Dictionary<string, int>();
        var unknownSounds = new HashSet<string>();
        var seenCuts = new HashSet<(int Region, double Step, string Sound)>();

        foreach (var (region, step, ev, isSound, _, visualOnly) in walk.Events)
        {
            // Reported only so a view can animate the slot; the walk already consumed them.
            if (visualOnly) continue;

            if (isSound)
            {
                if (CanonicalId(ev.SoundEvent!, soundMap) is { } sound)
                {
                    if (seenSounds.Add(sound)) soundOrder.Add(sound);
                    var pan = (ev as ExtendedEvent)?.Pan ?? 0f;
                    var offset = (ev as ExtendedEvent)?.OffsetInSeconds ?? 0d;
                    notes.Add(new WalkedNote(region, step, sound, ev.Value, ev.Volume, pan, offset));
                }
                else
                {
                    unknownSounds.Add(ev.SoundEvent!);
                }

                continue;
            }

            switch (ev)
            {
                // "!cut@a,b" / "#icut(a,b)": silence the named sounds.
                case IndividualCutEvent individualCut:
                {
                    foreach (var cutSound in individualCut.CutSounds)
                    {
                        if (CanonicalId(cutSound, soundMap) is not { } sound)
                        {
                            unknownSounds.Add(cutSound);
                            continue;
                        }

                        // Idempotent: repeating the same sound's cut at the same position
                        // (nothing else advancing position between them) collapses to one.
                        if (!seenCuts.Add((region, step, sound))) continue;

                        if (seenSounds.Add(sound)) soundOrder.Add(sound);
                        notes.Add(new WalkedNote(region, step, sound, 0, null, 0, 0, true));
                    }

                    break;
                }
                // Bare global cut - only sounds already introduced could be playing.
                case { SoundEvent: "!cut" }:
                {
                    foreach (var sound in soundOrder)
                    {
                        if (!seenCuts.Add((region, step, sound))) continue;
                        notes.Add(new WalkedNote(region, step, sound, 0, null, 0, 0, true));
                    }

                    break;
                }
                default:
                    ignoredEvents[ev.SoundEvent!] = ignoredEvents.GetValueOrDefault(ev.SoundEvent!) + 1;
                    break;
            }
        }

        if (notes.Count == 0)
            throw new InvalidOperationException("No sounds found in this sequence.");

        return new WalkData(walk.Regions, notes, soundOrder, ignoredEvents, [.. unknownSounds.Order()]);
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
    ///     of the other, since "!speed@2@x" is a subdivision change rather than a tempo change
    ///     and splitting there leaves a region with no musical subdivision that divides it. A
    ///     ratio that isn't whole (150 -> 90) is a real tempo change and starts a new segment.
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
    ///     The subdivision of the group's grid that leaves the fewest positions off it, rather
    ///     than the first exact fit: a few positions with an odd denominator (swing, humanized
    ///     stops) shouldn't force every other already-whole note onto a finer subdivision, whose
    ///     per-step integer sample truncation loses precision across the whole group. A length
    ///     that doesn't fit the grid is weighted far above any single note, since it drifts every
    ///     later group too.
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
    ///     that divides it.
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
                [.. notes.TakeWhile(p => p.Step < mainSteps)], quantized));

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
            [.. tailNotes.Select(p => (p.Step - mainSteps, p.Note))],
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
    ///     then whatever's left; anything unmusical is penalised heavily.
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
    ///     is kept tight because every candidate shape is a doubling of another one, and a wide
    ///     band would score both of them equally.
    /// </summary>
    private static double BpmPenalty(double bpm)
    {
        if (bpm is >= 70 and <= 190) return 0;
        return 3 * Math.Abs(Math.Log2(bpm / (bpm < 70 ? 70 : 190)));
    }

    // ---- Phase C: instruments and notes ----

    /// <param name="created">
    ///     Filled with the instruments this actually added - the ones an undo of the import
    ///     may remove again. An instrument that was already in the project is reused, not
    ///     listed here, and must survive the undo.
    /// </param>
    private static Dictionary<string, Instrument> BuildInstruments(ThirtyDollarProject project,
        List<string> soundOrder, List<Instrument> created)
    {
        var instruments = new Dictionary<string, Instrument>();
        foreach (var sound in soundOrder)
        {
            var candidate = new Instrument();
            candidate.AddSound(sound);
            instruments[sound] = Adopt(project, candidate, sound, created);
        }

        return instruments;
    }

    /// <summary>
    ///     The project's instrument for exactly these sounds, adding one when it has none, so a
    ///     track-kind conversion round trips without growing the instrument list.
    /// </summary>
    private static Instrument Adopt(ThirtyDollarProject project, Instrument candidate, string name,
        List<Instrument> created)
    {
        if (project.Instruments.FirstOrDefault(existing => SameSounds(existing, candidate)) is { } match)
            return match;

        var instrument = project.NewInstrument(UniqueName($"{DisplayName(name)} - imported", ExistingNames(project)));
        foreach (var sound in candidate.Sounds) instrument.Sounds.Add(sound);
        created.Add(instrument);
        return instrument;
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