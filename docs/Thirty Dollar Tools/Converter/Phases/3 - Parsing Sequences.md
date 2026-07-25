# Phase 3 — Parsing Sequences

> Owning code: `ThirtyDollarConverter.Parser/Sequence.cs`, `BaseEvent.cs`, `NormalEvent.cs`, `ValueScale.cs`, `Sound.cs`, and the `Custom Events/` folder.

A TDW save file is just a UTF-8 text blob — a `|`-separated list of "event tokens" describing which sound to play, at what pitch, how loud, when to loop, etc. Phase 3 parses that text into a strongly-typed `Sequence` of `BaseEvent`s ready for [[4 - Calculating the Placement|Calculating the Placement]].

The single entry point is `Sequence.FromString(string data)`.

## The `Sequence` object

```csharp
public partial class Sequence
{
    public BaseEvent[]                    Events     { get; set; } = [];
    public Dictionary<string, BaseEvent[]> Definitions = new();
    public HashSet<string>                 SeparatedChannels = [];
    public HashSet<string>                 UsedSounds        = [];
    public bool                            IsNewFormat { get; set; } = true;

    public Sequence Copy();
    public static Sequence FromString(string data);
}
```

| Field | Purpose |
| --- | --- |
| `Events` | The flat, expanded array of events. Macros (`#define`) are already inlined here. |
| `Definitions` | The named macros parsed out of `#define(...)` blocks. Kept around for reference but not used during encoding (the events from a definition are already inlined into `Events`). |
| `SeparatedChannels` | Names of sounds that should be rendered into their own track inside the `AudioMixer`. Set automatically by `#icut` / `!cut@…`. |
| `UsedSounds` | Hash of every sound name referenced — used by GUI/Visualizer to know what to download. |
| `IsNewFormat` | `true` for the post-rewrite TDW format (pan stored as `^val/100`), `false` for legacy (`#icut` syntax forces this off). |

## Top-level grammar

`FromString` splits on the pipe character `|`. Each piece, after `Trim()` and removing newlines, is one event token. Tokens come in three families:

1. **Sound events** — `🥁`, `tab@12`, `kick%80@5x^-0.5>0.25`
2. **Action events** — `!speed@200`, `!loop=4`, `!cut`, `!stop@2`, `!bg@#FF0000FF,1.5`, …
3. **Hash directives** — `#define(name)…#enddefine`, `#icut(a,b,c)`, `#bookmark(3)`

```csharp
while (enumerator.MoveNext()) {
    var text = enumerator.Current.Replace("\n","").Trim();
    if (string.IsNullOrEmpty(text)) continue;

    if (text.StartsWith('#') && TryDefine(text, enumerator, sequence)) continue;

    var new_event = ParseEvent(text, sequence);
    var repeats   = new_event.PlayTimes;
    new_event.PlayTimes = 1;

    sequence.UsedSounds.Add(new_event.SoundEvent);

    for (int i = 0; i < repeats; i++) {
        if (ProcessDefines(sequence, new_event, list)) continue;
        list.Add(new_event.Copy());
    }
}
```

### Repeats (`=N`)

If a token has a `=N` suffix (matched by `LoopTimesRegex` `=[0-9]+`), the parser sets `PlayTimes = N` on the parsed event, then **expands** it into `N` copies in the final array. The `PlayTimes` field on the copies is reset to `1`. (This is purely a parse-time expansion — `PlayTimes > 1` does **not** reach the placement calculator.)

### `#define` / `#enddefine`

```text
#define(piano_roll)
🎹@0
🎹@4
🎹@7
#enddefine
```

`TryDefine` matches the regex `^#(?<name>[^\s(]+)\((?<value>[^)]+)\)` against the line. If `name == "define"`, the parser advances the enumerator and calls `ParseDefines` to consume events until it sees `#enddefine`. The resulting `BaseEvent[]` is stored as `sequence.Definitions["piano_roll"] = […]`.

Later, when the parser sees an event whose `SoundEvent` matches a defined name, `ProcessDefines` inlines the definition into the output array — applying any pitch / volume / pan / offset modifiers from the call site to every event in the body.

The merging rules in `ProcessDefines` are nuanced:

- If the call is "vanilla" (`Value == 0`, default scale, default volume, no pan, no offset) → the definition's events are added verbatim.
- Otherwise: each non-action event in the body has the call's `Value` applied (per `ValueScale`), `Volume` multiplied (`*= newVolume / 100`), and `Pan`/`OffsetInSeconds` summed (clamped to `[-1, 1]` and `>= 0` respectively). When a body event was a `NormalEvent`, it's promoted to an `ExtendedEvent` so it can carry pan/offset.

### `#icut(a,b,c)` and `!cut@a,b,c`

Both forms produce an `IndividualCutEvent` with a `HashSet<string>` of sound names to silence. The tokenizer goes through `TryIndividualCut` (regex `^#icut\((?<events>[^)]+)\)`) for the `#icut` syntax, and `TryIndividualCutTDW` for the legacy `!cut@…` syntax.

A side-effect: every cut sound is added to `sequence.SeparatedChannels`, which tells the encoder to mix that sound into a *separate* track inside the `AudioMixer` so it can be silenced independently. (See [[5 - Encoding|Encoding]] for how that affects mixer track creation.)

`#icut` additionally flips `sequence.IsNewFormat = false`.

### `#bookmark(N)`

Produces a `BookmarkEvent` with `Value = N`. Implements both `ICustomActionEvent` and `IHiddenEvent`, meaning the placement calculator emits a placement for it but it has no audio side-effect (it's used by the Visualizer for jump labels).

## `ParseEvent` — the per-token regex pipeline

For ordinary events the parser runs a series of `[GeneratedRegex]` matchers over the token. They all use `CultureInfo.InvariantCulture` for number parsing.

| Field | Regex | Format | Captured into |
| --- | --- | --- | --- |
| sound name | `^[^@%^=>]*` | leading text | `SoundEvent` |
| value      | `@[-0-9.]+`  | `@12.5`     | `Value` (double) |
| value scale | `@[-0-9.]+@[^@%^=]+` | trailing `@x`/`@/`/`@+` | `ValueScale` |
| repeats     | `=[0-9]+`    | `=4`        | `PlayTimes` |
| volume      | `%[-0-9.]+`  | `%80`       | `Volume` (double?) |
| pan         | `\^[-0-9.]+` | `^-0.5`     | `Pan` (float)  → forces `ExtendedEvent` |
| offset      | `>[-0-9.]+`  | `>0.25`     | `OffsetInSeconds` (double) → forces `ExtendedEvent` |

If the token has neither `^` nor `>`, the result is a [[#NormalEvent|NormalEvent]]. If either is present, the parser instead constructs an [[#ExtendedEvent|ExtendedEvent]] so the extra fields can be carried through.

Special routes inside `ParseEvent`:

- `!bg@…` and `!pulse@…` are **not** parsed by the generic regex pipeline — they go through `ParseColorEvent` which encodes the RGBA + fade-time (or pulse count + frequency) into the single `double Value` field using a hand-rolled bit packing (see below).
- `#bookmark` is short-circuited at the end — `sound == "#bookmark"` returns a `BookmarkEvent`.

### Pan normalization (new vs old format)

```csharp
Pan = sequence.IsNewFormat ? pan / 100f : pan
```

The post-rewrite TDW website stores pan as a percentage (`^50` ≡ 50% right ≡ `0.5f` internally). The legacy format stored it as the raw `[-1, 1]` value already. `IsNewFormat` defaults to `true` and is flipped off when `#icut(...)` is encountered (which only ever appears in legacy saves).

### `!bg` and `!pulse` packing

Color events pack their state into the `double Value` field of a `NormalEvent` so the encoder can pass them through unchanged:

- **`!bg@#RRGGBB[AA],fadeSeconds`** — RGBA bytes are packed into the low 32 bits, `(fadeSeconds * 1000)` (clamped to `[0, 128000]`) is packed into the high 32 bits. `NormalEvent.Stringify()` knows how to round-trip this back to the textual form.
- **`!pulse@count,frequency`** — `(short)count << 8 | (byte)frequency`. Round-trip is `(short)(value >> 8)` for count and `(byte)value` for frequency, with frequency multiplied by `1000/5 = 200` on the way back out.

These details only matter to the Visualizer; the audio encoder ignores both events.

## The `BaseEvent` hierarchy

```text
BaseEvent (abstract)                    – Sequence.cs
├── NormalEvent                          – every plain audible / action event
│     └── ExtendedEvent                  – + Pan, + OffsetInSeconds (audible only)
│
├── IndividualCutEvent                   – #icut / !cut@…  ; ICustomActionEvent + ICustomAudibleEvent
├── BookmarkEvent                        – #bookmark        ; ICustomActionEvent + IHiddenEvent
└── EndEvent                             – synthetic "end of sequence" marker
                                          ; ICustomActionEvent + IHiddenEvent
```

Marker interfaces in `Custom Events/`:

- `ICustomActionEvent` — "this is a custom event we shouldn't run through the regular event-string switch in the placement calculator". The calculator branches on this to emit a placement and continue without trying to interpret it as `!speed`, `!loop`, …
- `ICustomAudibleEvent` — "this event participates in the audio render" (cuts and extended events). Used by both the calculator and the encoder to decide whether to mark the placement `Audible`.
- `IHiddenEvent` — "do not show this event in the Visualizer UI". Bookmarks and end markers carry this.

### `BaseEvent`

```csharp
public abstract class BaseEvent
{
    public float        PlayTimes  = 1;     // repeats (`=N`); collapsed to 1 after parse.
    public string?      SoundEvent;          // canonical event name ("🥁", "!speed", "#bookmark", …)
    public bool         Triggered;           // set true after a one-shot !loop / !jump fires
    public double       Value;               // semitones (sound) or argument (action)
    public ValueScale   ValueScale;          // None / Add / Times / Divide
    public double?      Volume;              // null → "use sequence default"
    public double       WorkingVolume = 100; // scratch field for placement calc
    public double       WorkingValue { get; set; } // scratch field for placement calc

    public abstract BaseEvent Copy();
    public void   Deconstruct(out string? name, out double value);
    public void   Deconstruct(out string? name, out double value, out double volume);
    public virtual string Stringify();
}
```

Notes on individual fields:

- **`SoundEvent`**: matches one of (a) a TDW sound name like `🥁` or `tab`, (b) an action starting with `!` like `!speed`, `!loop`, or (c) a hidden event starting with `#` like `#bookmark`, `#sequence_end`.
- **`Value`** is overloaded by event type:
  - For sound events → semitone offset (`@12` = +1 octave).
  - For `!speed` → BPM operand.
  - For `!volume` → percent operand.
  - For `!loop`/`!loopmany` → repeat count operand.
  - For `!jump` / `!target` → label id (matched as `Math.Abs(a-b) < 0.001f`).
  - For `!stop` → number of beats to wait (fractional supported).
  - For `!transpose` → semitone offset operand.
  - For `!bg` / `!pulse` → bit-packed visual state (above).
- **`ValueScale`** decides how the operand is applied to a *running* value (BPM, volume, transpose):

  ```csharp
  public enum ValueScale { Divide, Times, Add, None }
  ```

  - `None` → assignment (`bpm = value`)
  - `Add` → addition (`bpm += value`)
  - `Times` → multiplication
  - `Divide` → division

- **`WorkingVolume`** / **`WorkingValue`** / **`Triggered`** are scratch fields the placement calculator mutates while walking the sequence — they only make sense inside that loop and aren't part of the persisted format.
- **`Stringify()`** produces a TDW-text representation. `NormalEvent.Stringify` adds custom round-tripping for `!bg`, `!pulse` and `!divider`. `ExtendedEvent.Stringify` appends `^pan` and/or `>offset` if non-zero. `IndividualCutEvent.Stringify` joins each cut into `!cut@sound` tokens with `|` (so a sequence of cuts can be re-serialized).

### `NormalEvent`

The default concrete event. Plain `Copy()` clones every field. Holds anything that has no pan/offset.

### `ExtendedEvent` (≤ `NormalEvent`, implements `ICustomAudibleEvent`)

```csharp
public class ExtendedEvent : NormalEvent, ICustomAudibleEvent
{
    public bool   IsStandardImplementation { get; set; }
    public float  Pan { get; set; }           // -1 left, 0 center, 1 right
    public double OffsetInSeconds { get; set; } // start playback this many seconds in
    public float  TDWPan => Pan * 10;          // for visualizer scaling
}
```

`OffsetInSeconds` lets a sample start partway through. The encoder converts it to a sample offset using the *event-rate-adjusted* sample rate (i.e., factoring in semitone pitch shift): `offsetInSamples = startOffset * (sampleRate / 2^(value/12))`. See `PCMEncoder.RenderEventToSlice`.

`IsStandardImplementation` differentiates between:

- `true` — the event came from the post-rewrite format and pan is already pre-divided by 100.
- `false` — the event came from the legacy format (or from `#icut` body) and pan was supplied raw.

### `IndividualCutEvent`

```csharp
public class IndividualCutEvent : BaseEvent, ICustomActionEvent, ICustomAudibleEvent
{
    public readonly HashSet<string> CutSounds;
    public bool IsStandardImplementation { get; set; }
}
```

Carries which sounds to silence. `SoundEvent` is set to `"!cut"` (new-format) or `"#icut"` (legacy). The encoder's `RenderEventToSlice` looks at this set and runs `HandleCut` against the matching tracks in the `AudioMixer`.

### `BookmarkEvent` and `EndEvent`

Both implement `IHiddenEvent + ICustomActionEvent`. They are emitted as placements (so the Visualizer can render labels) but never trigger an audio sample. `EndEvent` is appended automatically by the placement calculator at the very end of every sequence.

## `Sound`

Sounds (the `Sound` POCO from `Sound.cs`) come from `sounds.json`:

```csharp
public class Sound
{
    public required string Id     { get; init; }
    public string?         Emoji  { get; init; }
    public string?         Name   { get; init; }
    public string?         Source { get; init; }
    public bool            UseID  { get; set; }

    public string Filename => UseID ? Id : Emoji ?? Id;
    public string IconUrl  => Emoji == null
        ? $"https://thirtydollar.website/icons/{Id}.png"
        : $"https://cdnjs.cloudflare.com/ajax/libs/twemoji/14.0.2/72x72/{codepoint}.png";
}
```

`Filename` is the string the parser actually sees in a sequence. The `SampleHolder.StringToSoundReferences` dictionary maps both `Id` and `Emoji` to the same `Sound`, so the encoder can later look up either alias.

## What Phase 3 hands off

A `Sequence` whose `Events` array is a flat, fully-expanded list of `BaseEvent`s — definitions inlined, repeats expanded, format quirks normalized. The next phase, [[4 - Calculating the Placement|Calculating the Placement]], walks that array as a tiny VM to produce a sample-accurate timeline.

---

**Previous:** [[2 - Loading Into Memory|Loading Into Memory]]
**Next:** [[4 - Calculating the Placement|Calculating the Placement]]
**Up:** [[../Converter|Converter]]
