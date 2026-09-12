# Auto-resume: repairing notes cut by another note's automation — proposal

**Status:** built, September 2026. Phases 1-6 of §7 are done; the sections below are the
design they were built from, and §9 records where the build deviates from them. Follows on from `editor-long-notes-plan.md`,
whose §1 already names the cause and whose §3 (Auto offset) already contains the mechanism.

**Goal:** when a long note is silenced by a cut that belongs to *another* note, the editor
puts it back automatically — instead of the user hand-tuning `Grid offset` until the two
retrigger grids happen to coincide.

---

## 1. The problem, precisely

A TDW cut is **by sound name**: `IndividualCutEvent` carries a set of sound names and
silences every instance of those sounds that is currently ringing, on any note, at any
pitch. `AudioKeyframeManager.Expand` emits one `GeneratedCutEvent` immediately before each
retrigger (`Cut`) and one at the note's end (`CutAtEnd`).

So two long notes on the same instrument that overlap in time interfere by construction:

```
step   0     1     2     3     4     5
A      [====:=====:=====:=====]              gap 1.5, End 4.5, cuts at 1.5, 3.0, 4.5
B            [====:=====:=====:=====]        gap 1.5, End 4.5, cuts at 2.5, 4.0, 5.5
                ^     ^     ^
                B's cut at 2.5 silences A. A's cut at 3.0 silences B. And so on.
```

`SequenceBuilder.HoistCuts` already solves the *aligned* case: cuts landing on the same
instant merge into one that fires before the whole step, so several voices retriggering
together cut once and all re-enter. `AudioKeyframeManager.AutomationOffset` (labelled
**Grid offset** in the inspector) exists so the user can shift one voice's grid onto
another's and reach that aligned case by hand.

Grid offset cannot cover:

- **different gaps** — no offset aligns a gap of 1 with a gap of 1.5;
- **a start distance that is not a whole number of gaps** — the case above, one step apart
  at gap 1.5;
- **`CutAtEnd`** — the end cut lands wherever the note ends, which is not on any other
  note's grid unless the lengths were chosen to make it so;
- **notes in different tracks or segments** that share an instrument, where the two grids
  are not even expressed in the same step rate.

And even where it *can* be reached, it is manual work per note, and re-doing it every time
a note moves.

### What cannot be the fix

- **Narrowing the cut** — TDW has no "cut this instance"; the name is the only selector.
- **Doing it in the encoder** — the exported sequence is meant to play on
  thirtydollar.website, so the repair has to exist as events in the sequence text.

So the only available repair is: play the sound again.

---

## 2. The fix

**Auto-resume.** When a generated cut silences a note that is still inside its declared
length, re-place that note's cut sounds at the same instant.

Where the automation has **Auto offset** on, the resume continues the sample from where it
had reached, which is seamless. Where it is off, the resume restarts the sound — which is
what that note's own retriggers already sound like, so the repair matches the note's own
character rather than quietly introducing a splice the user never asked for. Auto offset is
therefore the switch that decides whether a long note survives a foreign cut *invisibly* or
*audibly*, exactly as it decides that for the note's own retriggers.

The continuation, where it applies, is `AudioKeyframe.AutoOffset` (long-notes plan §3)
pointed at somebody else's cut instead of the note's own retrigger, and it reuses that
machinery whole:

```
resume = the ringing generated note, with
         AutoOffsetSeconds = ringing.AutoOffsetSeconds + (t - ringingStart) * 60 * 2^(ringing.Value / 12)
```

`Note.ToEvents()` already scales `AutoOffsetSeconds` per instrument sound by
`2^(sound.Value / 12)`, which is the encoder's own conversion. No new arithmetic, no
encoder change, nothing new in `PCMEncoder`.

Ordering is already right: `HoistCuts` puts a step's generated cuts at its front, so a
resume placed at exactly the cut's instant is emitted after the cut and re-enters cleanly.

### The rules, in full

1. **Only a note with `Automation` and `End > 0` resumes.** A plain note has no declared
   length, and the library does not know how long its samples are, so "still ringing" is
   not a question it can answer.
2. **Only strictly inside `(note start, End)`.** A cut exactly on `End` is the note's own
   ending. With `CutAtEnd` off a note bleeds past `End`; past `End` it is on its own, like
   a plain note.
3. **Only across `GeneratedCutEvent`s.** A cut the user wrote — a cut note, a faithful
   `!cut` — still stops the sound dead. That is also the escape hatch: to choke a ringing
   note deliberately, place a cut note.
4. **Only the sounds the cut actually names come back.** An instrument sound the cut misses
   is still ringing and must not be re-placed on top of itself.
5. **A cut at an instant where the note restarts itself is not a resume** — the retrigger
   already covers it. This is what keeps an aligned grid producing byte-identical output to
   today.
6. **The resume continues the sample only where the ringing instance says to.** That is the
   `AutoOffset` of the keyframe that started the instance, and `Template.AutoOffset` for the
   note's own first instance, before any keyframe. Off: the resume plays from that
   instance's own `Offset`, i.e. the sound restarts.
7. **Only `Note.Automation` resumes**, not `TrackAutomation`: the two expand against the
   same note in parallel, and resumes from both would double.
8. **One ringing instance is tracked, the most recent.** With the automation's `Cut` off,
   instances pile up and a foreign cut silences all of them; only the newest is restored.

---

## 3. Where it lands in the code

### The generation walk

```csharp
/// <summary>A cut somewhere on the timeline: what it silences, and whether the editor generated it.</summary>
internal readonly record struct CutPoint(double Minutes, IReadOnlySet<string> Sounds, bool Generated);

/// <summary>One instance this automation starts: a keyframe retrigger, or a resume repairing a foreign cut.</summary>
public readonly record struct GeneratedNote(double Minutes, Note Note, int KeyframeIndex, IReadOnlySet<string>? Sounds);

public IEnumerable<GeneratedNote> ExpandNotes(
    Note note, double noteMinutes, double stepMinutes, IReadOnlyList<CutPoint>? cuts = null)

public IEnumerable<(double Minutes, BaseEvent Event)> Expand(
    Note note, double noteMinutes, double stepMinutes, IReadOnlyList<CutPoint>? cuts = null)
```

`ExpandNotes` keeps being the single walk — it is what both the audio and the note editor's
`AutomationPath` read — and gains the resumes inline, in time order. `KeyframeIndex` is the
keyframe's index for a retrigger and **-1 for a resume**; `Sounds` is null for a retrigger
(the whole instrument) and the cut's sound set for a resume (rule 4). Between one own-start
and the next — or `End` for the last — it yields a resume for every `CutPoint` in the open
interval whose `Sounds` meet the instrument's, stopping at the first non-generated one.

`Expand` then emits, per yielded instance: a `GeneratedCutEvent` first when `Cut` and this
is a retrigger, then `ToEvents()` filtered by `Sounds`. A resume emits no cut of its own —
the foreign cut it repairs is already there, and already hoisted in front of it.

Changing `ExpandNotes`' yield type touches its two callers (`Expand`, `AutomationPath.Draw`)
and `AudioKeyframeTests`.

### The two passes

`cuts` has to be known before the notes expand, and a cut can come from any track in the
project. Rather than duplicating the cut-generation rules in a second place, the cut list is
harvested from a first, ordinary expansion:

```
pass 1: flatten as today (cuts: null), keep every IndividualCutEvent as a CutPoint
        (Generated = ev is GeneratedCutEvent)
pass 2: flatten again, passing the list
```

in exactly two callers:

- `ProjectTrack.ToSequence` — the note editor's single-track preview, own cuts only;
- `ThirtyDollarProject.BuildSequence` — which **already** gathers project-wide cuts: it
  injects every other placement's `IndividualCutEvent`s for cross-channel parity. Harvesting
  the list after that injection means per-channel playback and the merged export see the
  same cuts and therefore generate the same resumes, which is the invariant the existing
  comment there is protecting.

`ProjectTrack.TimedNotes` gains the same optional parameter and passes it down to
`note.Automation.Expand` only.

`FaithfulTrack` and `WaveTrack` are untouched. A faithful `!cut` enters the merged list as a
non-generated `CutPoint`, so it stops a piano-roll long note exactly as it does today.

### Cost

One extra expansion pass per sequence build. Notes flatten in microseconds next to
encoding, and the playback loop already re-expands the whole project on every edit. Marked
with a `ponytail:` comment; measure before doing anything cleverer.

Sequence length grows by one event per cut, per sound cut. A note retriggering at gap 0.25
across a 64-step neighbour can add 256 events to that neighbour. **Grid offset stays** — it
is now an optimisation ("align the grids and no resumes are generated at all") rather than
the only way to get correct audio.

---

## 4. On the grid

Resume points are drawn, so a note that is being repaired says so rather than silently
inflating the export.

`AutomationPath.Draw` already walks `ExpandNotes` and plots a tick per instance plus the
leaning line between consecutive ones. Feeding it the cut list makes resumes fall out of the
same walk:

- a resume tick is **half height and drawn in the end-cap colour**, so it reads as "the
  automation did something to this sound's continuity" without looking like a keyframe the
  user can grab. (A dedicated `automation-resume-color` in `GridViews.snx.ss` is one line if
  the shared colour reads badly against a busy chart.)
- **no `MarkerHandle`** for it — `KeyframeIndex == -1` is the test. Resumes are automatic
  and carry nothing to edit, so the `KeyframeBlock` pool must not hang a grab box on one, or
  it would swallow presses meant for the note body.
- the connecting line runs through resume points unchanged. A resume does not move the
  value, so the segment through it is flat, which is honest.
- they take slots from the same `AutomationMarkReserve` (768) and are culled to the viewport
  the same way.

The view's cut list is cached against `EditorState.Revision` and rebuilt only when the
revision changes, the same trick `TrackEditorView`'s longest-note rescan already uses
(`TrackEditorView.cs:766`) — O(a track expansion) per edit, nothing per frame.

The note editor shows one track, so it plots the cuts of that track only. A resume caused by
another track's note appears in the export and in playback but not on this grid; that is the
same single-track approximation the view already makes everywhere else.

---

## 5. Accepted artefacts

- **+6 dB for 4 ms at each splice**, where Auto offset is on. `SampleMixer.HandleCut` fades
  the cut instance out over `EncoderSettings.CutFadeLengthMs` while the resume starts at full
  level. Identical to what Auto offset already accepts (long-notes decision 11); a real
  crossfade would change how every offset event in every project renders.
- **Up to one sample of drift per splice**, from the encoder rounding `offset * sample_rate`
  to a whole sample. Also already true of Auto offset.
- **An audible restart where Auto offset is off** (rule 6). A held note with `Gap = 0` and
  Auto offset off restarts its sample from the beginning when a foreign cut hits it. Turning
  Auto offset on is the fix, and it is the same switch that makes the note's own retriggers
  seamless.

---

## 6. The toggle

`ThirtyDollarProject.AutoResume`, `bool`, default `true`; serialized as a trailing nullable
on `ProjectDto` so old files stay loadable, with **a missing key reading as on**. Existing
projects therefore change how they render the moment this ships — the notes they were
cutting now sustain — which is the point, and the checkbox is the way back. Inspector: one
`_form.CheckRow("Auto resume", …)` in the Project section under `Transpose`.

---

## 7. Phases

| # | Work | Done when |
|---|---|---|
| 1 ✓ | `CutPoint`, `GeneratedNote`, the resume scan in `ExpandNotes`, `Expand` emitting them | `AutoResumeTests`: two notes a step apart at gap 1.5 resume each other at the right offsets; an aligned pair generates no resumes at all; a user cut stops the sound; a partial sound-set overlap brings back only the cut sounds; nothing outside `(start, End)`; Auto offset off restarts instead of continuing |
| 2 ✓ | Two-pass wiring in `ProjectTrack.ToSequence` and `ThirtyDollarProject.BuildSequence`, the project flag, serialization | `ChannelSequence` and the merged export generate the same resumes; a project round-trips with the flag; the flag off reproduces today's sequence exactly; a file without the key loads with it on |
| 3 ✓ | Render parity | Render a long note alone, then the same note with an interfering neighbour on its instrument; with Auto offset on the two buffers match outside the 4 ms cut windows — the `AutoOffsetTests` shape, at values 0 and 12 |
| 4 ✓ | `AutomationPath` resume ticks | View tests: a resume tick appears in `Marks` at the foreign cut's x, in the end-cap colour, half height, and `Handles` does not grow; the connecting line still reaches the next keyframe |
| 5 ✓ | Inspector checkbox | It flips the flag through `EditorState.Edit`, undoes, and re-renders |
| 6 ✓ | Incremental render + an e2e look at 1080×720 through the `visualizer-headless` skill | `IncrementalRenderTests`: resizing one note changes its neighbour's resumes and the incremental result stays sample-identical to a full render; a screenshot of two interfering long notes showing their resume ticks |

---

## 8. Deliberately not built (say the word and it goes in)

- Resuming plain notes with no automation.
- A per-note "let this one be cut" flag — a cut note already says that.
- `TrackAutomation` resumes.
- Restoring more than the newest instance when the automation's `Cut` is off.
- A real crossfade at the splice.
- Plotting resumes caused by *another track's* notes in the single-track note editor.
- Detecting the aligned case and suggesting a `Grid offset` that would remove the resumes.
- A status-bar count of how many events auto-resume added to the export.

---

## 9. As built — phase 1

- **`CutPoint` and `GeneratedNote` are public.** `Expand` and `ExpandNotes` are public, so a
  parameter or yield type of theirs cannot be internal. Nothing internal leaks through them:
  "the editor generated this cut" is a bool on `CutPoint`, not the `GeneratedCutEvent` type.
- **`AudioKeyframeManager.CoincidentMinutes` (1e-9)** is how close two instants have to be to
  count as one, for the two open ends of a resume stretch. A foreign cut that coincides with
  an own start is computed down a different path — another note's step, another segment's
  offset — so it is only equal to within float noise, while `SequenceBuilder` rounds both to
  the same step of the exported sequence anyway.
- **`Resumes` is a static local of its own** rather than inline in the `ExpandNotes` loop: the
  stretch after the last keyframe needs the same walk, and a cut the user wrote ends a stretch
  with a `yield break` that must not end the whole expansion.
- **`AutomationPath` was left at today's behaviour** (it passes no cuts), only re-shaped for
  the new yield type — its own keyframe index now comes from `GeneratedNote` instead of a
  counter it kept. Phase 4 is what makes it draw resumes.
- Nothing calls `Expand` with a cut list yet, so this phase changes no rendered output.
  `AutoResumeTests` builds its cut lists by hand, or from the interfering note's own `Expand`.

## 10. As built - phases 2-6

- **`ProjectTrack.ToSequence` gained `bool autoResume = true`** rather than a back-reference
  to the project: a track does not know which project holds it. The only caller that matters
  is `EditorState.ConvertTrack` (piano roll <-> faithful), which passes the project's flag, so
  a converted track sounds like the one it replaced.
- **`SequenceBuilder.CutPoints`** is the one harvest, used by the project build, the
  single-track build and the public `ProjectTrack.CutPoints()` the note editor draws from.
  Both builds skip the second pass when no cut on the timeline was generated - a project
  without a single long note pays one `Exists` call.
- **`ProjectTrack.CutPoints()` is public** so the view can harvest without the library's
  internals. It is the track's own cuts only; the project's build harvests the merged list.
- **The view walks in track-absolute minutes when it has a cut list**, and subtracts the note's
  own start when it plots - `AutomationPath` used to pass 0 and work note-relative. The note's
  absolute time comes from `MinutesAtStepPosition(GlobalStepOf(...))`, both already public.
  Across a tempo change this is the same display-only approximation the path already made.
- **Resumes stay off the connecting line.** Routing the lean through them would flatten the
  path the keyframes describe, so a resume draws its tick and nothing else. The tick sits at
  the value actually playing, which on a note whose value moves is a hair off the lean.
- **`TrackEditorView.KeyframeHandles`** is a new test seam next to `AutomationMarks`, for the
  "a resume gets no grab box" check.
- **The checkbox is in the Project section, so it is only reachable from the arrangement** -
  the section is hidden while a track is open, exactly like Transpose and BPM. Consistent with
  its neighbours, and worth knowing before hunting for it in the note editor.
- **The render parity test runs at 50 Hz, not 440.** The encoder truncates
  `offset * sample_rate` to a whole sample, so a splice can land one sample early; at 440 Hz
  that is 6% of full scale and swamps a 1e-4 comparison. At 50 Hz it is 0.7%, while an offset
  that is genuinely wrong still moves the phase by whole cycles. The exact offsets are pinned
  in `AutoResumeTests` instead, where they are integers rather than samples.
- **The interfering voice in that test is silent** (`Volume = 0`), so the two renders differ
  only in what happened to the note being repaired.

### Verified at 1080x720

Through the `visualizer-headless` skill: a 6-step held note crossed by a 4-step retriggering
one on the same instrument shows a row of half-height resume ticks along its body, stopping at
its end cap, while the interfering note keeps its own full-height keyframe markers and leaning
line. Unchecking "Auto resume" in the Project section and reopening the track leaves the held
note's body clean. The hint bar still wraps to three lines and fits.
