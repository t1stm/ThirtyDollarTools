# Phase 4 — Calculating the Placement

> Owning code: `ThirtyDollarConverter/PlacementCalculator.cs`, `Objects/Placement.cs`, `Objects/TimedEvents.cs`, `EventType.cs`.

A parsed `Sequence` is just a list of opcodes. To turn it into audio you need to know **at what audio-sample index** each event should be triggered, and you need to resolve all the control-flow opcodes (`!speed`, `!loop`, `!jump`, `!stop`, `!cut`, `!transpose`, `!volume`, `!combine`, …) into a flat schedule. That's what `PlacementCalculator` does — it's a tiny VM that walks the events and yields a `Placement` for each sample/action.

## What is a `Placement`?

```csharp
public class Placement : IEquatable<Placement>
{
    public bool      Audible       { get; init; } = true;
    public BaseEvent Event         { get; init; } = NormalEvent.Empty;
    public ulong     Index         { get; init; }   // audio-sample timeline index
    public ulong     SequenceIndex { get; init; }   // index back into Sequence.Events
}
```

- **`Index`** is the absolute sample position in the output buffer where the event fires — calculated using `SampleRate / (bpm/60)` (samples per beat).
- **`SequenceIndex`** points back to the source `BaseEvent` array — used by the Visualizer to highlight the playhead.
- **`Audible`** distinguishes events that should be rendered (sounds, cuts, extended events) from events that are purely metadata (speed changes, bookmarks, jump landings).

`Placement` implements `IEquatable<Placement>` with a fuzzy equality (sound name, value, volume, pan, offset, audible flag, index) that's used by the incremental render path in `PCMEncoder.ComputeIncrementalAudio` — see [[Encoding]].

## What is a `TimedEvents`?

```csharp
public class TimedEvents
{
    public Sequence[]  Sequences        { get; set; } = [];
    public Placement[] Placement        { get; set; } = [];
    public int         TimingSampleRate { get; set; } = 48000;
}
```

The bundle handed off to the encoder. It contains the raw sequences (so the encoder can iterate `SeparatedChannels` to allocate mixer tracks) **and** the final ordered placement timeline.

## Entry points

```csharp
public IEnumerable<Placement> CalculateOne (Sequence sequence, ulong? startTime = null);
public IEnumerable<Placement> CalculateMany(IEnumerable<Sequence> sequences);
```

`CalculateMany` is the multi-sequence wrapper: it concatenates sequences end-to-end, using the *last placement index* of one sequence as the `startTime` of the next. The result is a single sorted-by-`Index` list.

`CalculateOne` is where all the work happens. It is implemented as a `yield return` iterator so callers can stream placements without buffering the whole timeline.

## The VM state

```csharp
var bpm           = 300.0;
var transpose     = 0.0;
var global_volume = 100.0;
var position      = startTime ?? (ulong)(SampleRate / (bpm / 60));  // pre-rolled by 1 beat
var loop_target   = 0ul;
var index         = 0ul;
var scrubbing     = false;
var scrub_pos     = 0ul;
```

| Variable | Role |
| --- | --- |
| `bpm`           | Current tempo. Default **300 BPM**. Modified by `!speed`. Beat length in samples = `SampleRate / (bpm/60)`. |
| `transpose`     | Global pitch offset (semitones) added to every audible event's `Value`. Modified by `!transpose`. |
| `global_volume` | Sequence-wide volume in percent. Modified by `!volume`. Multiplied with each event's per-event volume to produce `WorkingVolume`. |
| `position`      | Current "play head" in audio samples. The next sound placed lands here. |
| `loop_target`   | Sample-array index of the most recently seen `!looptarget`. `!loop` and `!loopmany` rewind to this. |
| `index`         | Current cursor into `sequence.Events`. |
| `scrubbing`     | If true, suppress yielding placements until `index == scrub_pos`. (Reserved for future scrub support — not currently driven by any event.) |

## Per-event dispatch

For each event the VM classifies it as either an `EventType.Sound` (audible) or `EventType.Action`:

```csharp
var event_type = (ev.SoundEvent?.StartsWith('!') ?? true) || ev is ICustomActionEvent
    ? EventType.Action
    : EventType.Sound;
```

`ICustomActionEvent` (bookmarks, end markers, individual cuts) always count as Action regardless of the leading character.

### Sound events

```csharp
if (event_type == EventType.Sound)
{
    var next_event = index + 1 < count ? sequence.Events[index + 1].SoundEvent : null;
    increment_timer = next_event is not "!combine";

    var copy = ev.Copy();
    var event_volume = copy.Volume ??= 100;
    copy.WorkingVolume = global_volume * event_volume / 100d;
    copy.Value += transpose;

    yield return new Placement {
        Index = position,
        SequenceIndex = index,
        Event = copy,
        Audible = ev.SoundEvent is not "_pause"
    };

    if (increment_timer) position += (ulong)(SampleRate / (bpm / 60));
    index++;
    continue;
}
```

For each sound event the calculator:

1. Copies the event (the iterator yields *this* copy — the original stays untouched on the parsed `Sequence`).
2. Resolves the **working volume** = `global_volume * (eventVolume ?? 100) / 100`.
3. Applies the **running transpose** to `Value`.
4. Looks ahead one event: if the next is `!combine`, the play head does **not** advance after this event — both events end up at the same `Index`. Otherwise advance by one beat (`SampleRate / (bpm/60)` samples).
5. Yields a placement, marking it `Audible = false` only if the special `_pause` sound is used (a literal "skip a beat with no sound" placeholder).

### Action events

A switch on `ev.SoundEvent` handles each action. Two flags drive the post-switch behavior:

- `default_return` — should the calculator emit a "neutral" placement at `position` for this action? (Set to `false` by actions that already yielded a custom placement.)
- `modify_index` — should the cursor advance to `index + 1`? (Set to `false` by jumps/loops that move the cursor manually.)
- `audible` — set `true` for actions that participate in audio (e.g. `!cut`, custom `ICustomAudibleEvent`s).

#### `!speed`

Modifies BPM according to `ValueScale`:

```csharp
case ValueScale.Divide: bpm /= ev.Value;
case ValueScale.Times:  bpm *= ev.Value;
case ValueScale.Add:    bpm += ev.Value;
case ValueScale.None:   bpm  = ev.Value;
```

Because `position += SampleRate / (bpm/60)` is recomputed every event, changing BPM affects only **subsequent** beats.

#### `!volume`

Modifies `global_volume` the same way `!speed` modifies `bpm` (clamped to ≥ 0). Yields its own non-audible placement carrying `WorkingVolume = global_volume` so that downstream consumers (Visualizer, debug views) can show the running volume.

#### `!stop`

```csharp
var working_value = ev.Value;
while (ev.WorkingValue > 0) {
    var multiplier = Math.Min(working_value, 1);
    position += (ulong)(multiplier * SampleRate / (bpm / 60));
    ev.WorkingValue -= 1;
    working_value   -= 1;
    if (AddVisualTimings)
        yield return new Placement { Index = position, ... Audible = false };
}
```

Pauses the play head for `Value` beats. The implementation handles fractional values by calling `Math.Min(working_value, 1)` per iteration — so `!stop@2.5` advances by `1, 1, 0.5` beats. When `EncoderSettings.AddVisualEvents` is true, each step also emits a hidden placement (used by the Visualizer to draw the pause animation frame-accurately).

#### `!loopmany`

```csharp
if (ev.WorkingValue > 0) {
    ev.WorkingValue--;
    yield return new Placement { ... Audible = false };
    index = loop_target;
    Untrigger(ref sequence, index, LoopmanyUntriggers);
}
```

Decrements its `WorkingValue` (initialized to `Value` by the parser) and rewinds `index` to `loop_target` once per pass. When `WorkingValue` hits 0 it falls through and execution continues past the loop. `Untrigger` resets all events between `loop_target` and the end to their pristine state — *except* events whose `SoundEvent` is in `LoopmanyUntriggers = [ "!loopmany" ]`, since those would otherwise re-arm themselves and the loop would never terminate.

#### `!loop`

```csharp
if (ev.Triggered) break;
ev.Triggered = true;
yield return new Placement { ... Audible = false };
index = loop_target;
Untrigger(ref sequence, index, LoopUntriggers);   // [ "!loopmany", "!loop" ]
```

A one-shot loop. `Triggered` flips to `true` after the first hit so subsequent passes fall through. `Untrigger` reactivates everything *except* `!loop`/`!loopmany` (so they don't fire again). Note `Triggered` is a regular field on `BaseEvent` — `Untrigger` resets it to `false` on every other event each pass.

#### `!jump` ↔ `!target`

```csharp
var item = sequence.Events.FirstOrDefault(r =>
    r.SoundEvent == "!target" &&
    Math.Abs(r.Value - ev.Value) < 0.001f &&
    !r.Triggered);
```

`!jump@N` searches the events array for an `!target@N` (matched fuzzily because `Value` is a `double`) that has not yet been triggered. If found, the cursor jumps there, the jump event is marked `Triggered`, and `Untrigger` resets every event except `!loop`/`!loopmany`/`!jump`/`!target` (`JumpUntriggers`) so loops can re-fire after the jump. If no untriggered target exists, the jump is a no-op (logged).

#### `!cut`

```csharp
case "!cut":
    audible = true;
    Log($"Cutting audio at: '{position + SampleRate / (bpm / 60)}'");
    break;
```

Marks the placement as audible; the encoder's `RenderEventToSlice` is the one that actually mutes every track at this index.

#### `!looptarget`

```csharp
case "!looptarget":
    loop_target = index;
    break;
```

Records the current cursor as the next rewind target for `!loop` / `!loopmany`.

#### `!transpose`

Mutates `transpose` per `ValueScale` (same shape as `!speed`/`!volume`). The new value is added to every subsequent sound event's `Value`.

#### Other actions

```csharp
case "" or "!flash" or "!bg" or "!combine" or "!startpos" or "!pulse" or "!target":
    break;
```

These are emitted as placements (via `default_return`) but have no effect on the VM state. `!combine` is special-cased earlier (when handling sounds — see "Sound events" above).

### `ICustomActionEvent` placements

If the switch falls through to `default_return`, the calculator yields:

```csharp
yield return new Placement {
    Index = position,
    SequenceIndex = index,
    Event = ev,
    Audible = audible        // true only for `ICustomActionEvent + ICustomAudibleEvent`
};
```

So `IndividualCutEvent` (which implements both interfaces) gets `Audible = true`; `BookmarkEvent` (action only) gets `Audible = false`.

### Cursor advance

After processing, `index++` (unless an action explicitly cleared `modify_index`) and `position += beat_length` if `increment_timer` was set. `!combine`, `!loop`, `!loopmany`, `!jump`, and any action that opted out via `default_return = false` skip the position advance for that step.

## The terminating `EndEvent`

```csharp
yield return new Placement {
    Index = position,
    SequenceIndex = index,
    Event = new EndEvent(),
    Audible = false
};
```

Always appended — gives the encoder a definitive "end of timeline" marker. The encoder uses `placement[^1].Index` to size the output buffer.

## `Untrigger(ref Sequence, ulong index, string[] except)`

```csharp
private static void Untrigger(ref Sequence sequence, ulong index, string[] except)
{
    if (index == 0) index++;
    for (var i = index - 1; i < (ulong)sequence.Events.LongLength; i++)
    {
        var current_event = sequence.Events[i];
        if (except.Any(r => r == current_event.SoundEvent)) continue;
        current_event.Triggered  = false;
        current_event.WorkingValue = current_event.Value;
    }
}
```

Walks from a starting index to the end of the sequence and reverts every event's `Triggered` flag and `WorkingValue` — except those whose `SoundEvent` appears in the `except` list. This is what makes loops/jumps re-firable when control flow rewinds past them.

There are three guard lists at the top of the file:

```csharp
private static readonly string[] JumpUntriggers     = ["!loop", "!loopmany", "!jump", "!target"];
private static readonly string[] LoopUntriggers     = ["!loopmany", "!loop"];
private static readonly string[] LoopmanyUntriggers = ["!loopmany"];
```

The pattern: each rewind operation guards the events that *caused* the rewind (so they don't infinitely re-fire) but resets everything else — including any nested loops/jumps inside the loop body.

## Multi-sequence timing in `CalculateMany`

```csharp
var last_end_index = 0ul;
foreach (var sequence in sequences) {
    var calculated = last_end_index == 0ul
        ? CalculateOne(sequence)
        : CalculateOne(sequence, last_end_index);

    var placements = calculated.OrderBy(c => c.Index).ToList();
    last_end_index = placements.Last().Index;
    list.AddRange(placements);
}
```

Each subsequent sequence's `position` starts at the last placement's `Index` of the previous sequence — so multiple covers concatenate seamlessly without overlap.

## What Phase 4 hands off

A `TimedEvents` containing:

- The original `Sequence[]` (so the encoder knows about `SeparatedChannels`).
- The flat ordered `Placement[]`.
- The `TimingSampleRate` (= encoder's output sample rate).

The encoder then uses `Placement.Index` directly as the position into the output mix buffer. See [[Encoding]].

---

**Previous:** [[Parsing Sequences]]
**Next:** [[Encoding]]
**Up:** [[../Converter|Converter]]
