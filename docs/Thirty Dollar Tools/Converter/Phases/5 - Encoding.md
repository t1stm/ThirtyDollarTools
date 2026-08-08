# Phase 5 — Encoding

> Owning code: `ThirtyDollarConverter/PCMEncoder.cs`, `SampleProcessor.cs`, `ProcessedEvent.cs`, `Objects/RenderedSequence.cs`, `Objects/EncoderSettings.cs`, plus `ThirtyDollarConverter.Encoder/PCM/AudioMixer.cs`, `ThirtyDollarConverter.Encoder/PCM/PercentageScale.cs`, `ThirtyDollarConverter.Encoder/Mixers/SampleMixer.cs`, and the resampler library.

This is the heaviest phase. Given a `TimedEvents` (the output of [Calculating the Placement](4%20-%20Calculating%20the%20Placement.md)) and the in-memory `SampleHolder` (from [Loading Into Memory](2%20-%20Loading%20Into%20Memory.md)), the encoder produces a fully-mixed stereo `AudioData<float>` and optionally writes it as a `.wav` file.

Conceptually it is a four-step pipeline:

```
Placement[]                         (from Phase 4)
       │
       │ A. SampleProcessor + IResampler
       ▼
Dictionary<(name, value), ProcessedEvent>
       │
       │ B. AudioMixer.AddTrack per separated channel
       ▼
AudioMixer (one track per !cut/icut "channel")
       │
       │ C. ProcessChannel × _channels    (parallel)
       │     └─ ProcessChunk × MultithreadingSlices  (parallel, SIMD)
       │         └─ RenderEventToSlice → RenderSample
       ▼
AudioMixer with all tracks summed
       │
       │ D. AudioMixer.MixDown() → AudioData<float>
       ▼
WriteAsWavFile
```

The orchestrator is `PcmEncoder`. The rest of this document walks each piece.

## `EncoderSettings`

```csharp
public class EncoderSettings
{
    public uint    SampleRate;                    // output sample rate (Hz)
    public uint    Channels;                      // 1 or 2 (else exception)
    public uint    CutFadeLengthMs    = 4;        // !cut fade length in ms
    public uint    CombineDelayMs     = 0;        // currently unused
    public bool    EnableNormalization = true;    // normalize on WriteAsWavFile
    public int     MultithreadingSlices = Environment.ProcessorCount * 4;
    public bool    ClampBpm;                      // clamp BPM to [5, 20000] after !speed, matching TDW (off by default)
    public bool    ClampVolume;                   // clamp global volume to [0, 600] after !volume, matching TDW (off by default)
    public bool    ClampTranspose;                // clamp running transpose to [-60, 60] after !transpose (off by default)
    public bool    ClampPitch;                    // clamp each note's final pitch to [-72, 72] (off by default)
    public bool    ClampNoteVolume;                // clamp each note's own volume ratio to [0, 4] (off by default)
    public IResampler Resampler        = new HannSincResampler();
    public PercentageScale VolumeScale = PercentageScale.LinearOverflowLogarithmic;
    public PercentageScale PanScale    = PercentageScale.EqualPower;
    public bool    AddVisualEvents;
    public string  DownloadLocation { get; set; } = "";
}
```

The constructor of `PcmEncoder` rejects `Channels < 1` and `Channels > 2` (anything other than mono / stereo throws). All other settings are runtime-configurable.

`PercentageScale` controls how a percentage (volume / pan) is mapped to a multiplier:

```csharp
public enum PercentageScale { Linear, LinearOverflowLogarithmic, Logarithmic, EqualPower }
```

| Value | Multiplier formula |
| --- | --- |
| `Linear` | `multiplier = pct / 100` (1:1) |
| `LinearOverflowLogarithmic` | linear up to 100%, `sqrt(pct/100)` past 100% |
| `Logarithmic` | always `sqrt(pct/100)` |
| `EqualPower` | pan only — constant-power cosine/sine curve, matching the Web Audio API's `StereoPannerNode` (the same curve the Thirty Dollar Website uses). Handled by a separate `RenderEqualPowerPan` path instead of the plain attenuation formula. |

Used both in `SampleMixer.RenderSample` (volume) and in pan attenuation (`PanScale`).

## `PcmEncoder` — the orchestrator

```csharp
public PcmEncoder(SampleHolder samples, EncoderSettings settings,
                  Action<string>? loggerAction = null,
                  Action<ulong, ulong>? indexReport = null)
```

Construction wires up:

- `_sampleProcessor = new SampleProcessor(samples.SampleList, settings, log)` — used in step A.
- `PlacementCalculator = new PlacementCalculator(settings, log)` — used by `GetSequenceAudio` / `GetMultipleSequencesAudio`.
- `_indexLock` — a `SemaphoreSlim(1)` used to serialize the progress reporter.

### Public entry points

```csharp
public Task<RenderedSequence> GetSequenceAudio(Sequence sequence);
public Task<RenderedSequence> GetMultipleSequencesAudio(IEnumerable<Sequence> sequences);
public Task<RenderedSequence> ComputeIncrementalAudio (RenderedSequence old, IEnumerable<Sequence> new);

public Task<AudioData<float>>           GetAudioFromTimedEvents       (TimedEvents, ...);
public Task<(AudioData<float>, AudioMixer)> GetAudioAndMixerFromTimedEvents(TimedEvents, ...);

public void WriteAsWavFile(string path,   AudioData<float> data);
public void WriteAsWavFile(Stream stream, AudioData<float> data);
```

`GetSequenceAudio` is the everyday path. It calls `GetMultipleSequencesAudio([seq])`, which:

1. Asks the `PlacementCalculator` for a sorted `Placement[]` (this is [Calculating the Placement](4%20-%20Calculating%20the%20Placement.md)).
2. Wraps it in a `TimedEvents`.
3. Calls `GetAudioSamples` (step A below) to resample each unique event.
4. Calls `GenerateAudioAndMixer` (steps B+C below) to produce the final `AudioMixer` and a flat `AudioData<float>`.
5. Returns a `RenderedSequence` bundle:

   ```csharp
   public class RenderedSequence
   {
       public required TimedEvents      TimedEvents      { get; set; }
       public required AudioData<float> Audio            { get; set; }
       public required uint             AudioSampleRate  { get; set; }
       public AudioMixer?               Mixer            { get; set; }
       public Dictionary<(string,double), ProcessedEvent>? ProcessedEvents { get; set; }

       public Sequence[]  Sequences => TimedEvents.Sequences;
       public Placement[] Placement => TimedEvents.Placement;
   }
   ```

   Both `Mixer` and `ProcessedEvents` are kept around so a subsequent edit can be incrementally re-rendered (see [the incremental render section](#incremental-rendering-computeincrementalaudio)).

## Step A — Resampling each unique event

`PcmEncoder.GetAudioSamples(TimedEvents, oldDict?)` is the resample step.

### Deduplicating events

Multiple placements of the same `(name, value)` pair share the same audio. The encoder builds a `Dictionary<(string, double), BaseEvent>` of *unique* (name, semitone) pairs, looking each up by canonical sound id (via `SampleHolder.StringToSoundReferences`):

```csharp
foreach (var p in placement) {
    if (!p.Audible) continue;
    var ev = p.Event;
    var event_name  = ev.SoundEvent ?? "";
    if (event_name == "!cut" || ev is ICustomActionEvent) continue;

    if (Holder.StringToSoundReferences.TryGetValue(event_name, out var sound))
        event_name = sound.Id;

    var key = (event_name, ev.Value);
    if (processed_events_dictionary.ContainsKey(key)) continue;
    to_process_dictionary.TryAdd(key, ev);
}
```

If `oldDict` is supplied (incremental render), already-resampled keys are skipped — this is what makes editing pitch on a single note cheap.

### The parallel resample

```csharp
ulong finished_tasks = 0;
var total_tasks = (ulong)processed_events.Length;

await Parallel.ForEachAsync(processed_events, async (processedEvent, token) => {
    processedEvent.ProcessAudioData(_sampleProcessor);
    await _indexLock.WaitAsync(token);
    finished_tasks++;
    IndexReport(finished_tasks, total_tasks);
    _indexLock.Release();
});
```

Each unique event is wrapped in a `ProcessedEvent` and farmed out to `Parallel.ForEachAsync`. The runtime's default parallelism (typically `ProcessorCount`) keeps every CPU busy. The `_indexLock` only serializes the *progress callback* — the actual resample is fully parallel.

`ProcessedEvent` is a thin wrapper around the resampled audio:

```csharp
public class ProcessedEvent(BaseEvent ev)
{
    public AudioData<float> AudioData = AudioData<float>.Empty(2);
    public string? Name  => ev.SoundEvent;
    public double  Value => ev.Value;

    public void ProcessAudioData(SampleProcessor processor)
        => AudioData = processor.ProcessEvent(ev);
}
```

### `SampleProcessor` — the actual resample

```csharp
public AudioData<float> ProcessEvent(BaseEvent ev)
{
    var (_, value) = _samples.FirstOrDefault(p =>
        p.Key.Filename == ev.SoundEvent || p.Key.Id == ev.SoundEvent);
    if (value == null) throw new Exception(...);

    var sampleData = value.ReadAsFloat32Array(_settings.Channels > 1);
    var audioData  = new AudioData<float>(_settings.Channels);

    for (var i = 0; i < _settings.Channels; i++)
        audioData.Samples[i] = Resampler.Resample(
            sampleData.GetChannel(i),
            value.SampleRate,
            (uint)(_settings.SampleRate / Math.Pow(2, ev.Value / 12)));

    return audioData;
}
```

The pitch math is the only "audio knowledge" in the encoder:

> **Target rate = `SampleRate / 2^(Value / 12)`**

So `@12` (one octave up) targets *half* the sample rate, which when later played back at the encoder's actual sample rate will sound twice as fast and one octave higher. A sound's `Value` is interpreted in semitones, and `12` semitones = one octave — hence `Pow(2, Value/12)`.

The mono → stereo flag (`_settings.Channels > 1`) is forwarded to `ReadAsFloat32Array`, ensuring single-channel sources are duplicated to both stereo channels before resampling.

### The `IResampler` family

| Resampler | Quality | Notes |
| --- | --- | --- |
| `NoInterpolationResampler` | Worst | Nearest-neighbor pixel duplicate. Fastest. |
| `LinearResampler` | Low | `lerp(span[i], span[i+1], frac)`. |
| `HermiteResampler` | Mid | 4-tap cubic Hermite. Default in CLI. |
| `HannSincResampler` | High | Bandlimited sinc (`filterSize=64, precision=512`) windowed by Hann. **Default in core.** |
| `KaiserFastResampler` | Mid-high | Kaiser-windowed sinc — fast variant. |
| `KaiserBestResampler` | Best | `beta=12.9846, rolloff=0.9173, zeros=50`. |
| `KaiserSincResampler` | High | Generic Kaiser-window sinc. |
| `ByteCruncherResampler` | Stylized | Lo-fi bit-crusher / aliasing artifact resampler. |

All implement:

```csharp
public interface IResampler {
    string Name { get; }
    float[]  Resample(Memory<float>  samples, uint sampleRate, uint targetSampleRate);
    double[] Resample(Memory<double> samples, uint sampleRate, uint targetSampleRate);
}
```

`HannSincResampler` precomputes a sinc lookup table at construction (`_table = sinc(t) * hann(t/filterSize)` for `t = i/precision`) and a paired `_delta` table for linear interpolation between table entries. The hot loop is:

```csharp
for (j = sample_index - filterSize; j <= sample_index + filterSize; j++) {
    if (j < 0 || j >= samples.Length) continue;
    var t   = Math.Abs(sample_position - j);
    var idx = (int)(t * _precision);
    var eta = t * _precision - idx;
    var window = samples[j] * (_table[idx] + eta * _delta[idx]);
    result += window;
}
```

`HermiteResampler` is the simpler 4-tap cubic:

```csharp
c0 = p[1]
c1 = 0.5 * (p[2] - p[0])
c2 = p[0] - 2.5 p[1] + 2 p[2] - 0.5 p[3]
c3 = 0.5 * (p[3] - p[0]) + 1.5 * (p[1] - p[2])
out = ((c3 * f + c2) * f + c1) * f + c0
```

The choice of resampler is a quality/speed tradeoff. The CLI defaults to `HermiteResampler` (Program.cs:59) for speed; the core library defaults to `HannSincResampler` for quality.

## Step B — Building the `AudioMixer`

Once every unique event is resampled, the encoder allocates the output buffer and a mixer:

```csharp
var last_placement   = events.Placement[^1];
var big_event        = processedEvents.Values.MaxBy(e => e.AudioData.GetLength());
var big_event_length = big_event?.AudioData.GetLength() ?? 0;
var length           = (int)last_placement.Index + big_event_length;
var audio_data       = AudioData<float>.WithLength(_channels, length);
var mixer            = new AudioMixer(audio_data);
```

The output length is the index of the final placement plus the longest single sample (so the longest tail can ring out fully).

Then it iterates `sequence.SeparatedChannels` (populated by [Parsing Sequences](3%20-%20Parsing%20Sequences.md) from `#icut` and `!cut@…`) and adds one **track per separated sound** to the mixer:

```csharp
foreach (var sequence in events.Sequences)
foreach (var channel in sequence.SeparatedChannels)
{
    if (mixer.HasTrack(channel)) continue;
    var channelID = Holder.StringToSoundReferences.TryGetValue(channel, out var sound) ? sound.Id : channel;
    var new_track = AudioData<float>.WithLength(_channels, length);
    mixer.AddTrack(channelID, new_track);
}
```

Why separate tracks? `!cut` and `#icut` need to silence specific sounds without affecting the global mix. Each cut-target lives on its own track; cuts mute that track's slice; the final mixdown sums everything.

### `AudioMixer`

```csharp
public class AudioMixer : IDisposable
{
    private readonly ConcurrentDictionary<(string trackName, AudioLayout layout), AudioData<float>> _tracks;
    public  readonly IMixingMethod MixingMethod = new BasicMixer();

    public AudioMixer(AudioData<float> defaultChannel, AudioLayout defaultLayout = AudioLayout.AudioLr);

    public AudioData<float> MixDown();
    public (AudioLayout, AudioData<float>)[] GetTracks();
    public bool             HasTrack(string sound, AudioLayout = AudioLr);
    public AudioData<float> GetTrackOrDefault(string trackName, AudioLayout = AudioLr);
    public AudioData<float> GetTrack(string trackName, AudioLayout = AudioLr);
    public bool             AddTrack(string trackName, AudioData<float>, AudioLayout = AudioLr);
    public AudioData<float> GetDefault();
    public int              GetLength();

    public void Sum(params ReadOnlySpan<AudioMixer> addMixer);
    public void Dispose();
}
```

- Tracks are keyed by `(trackName, AudioLayout)` — defaulting to `AudioLayout.AudioLr` (stereo).
- `GetTrackOrDefault(name)` returns the named track if present, otherwise the default `""` track. This is what `RenderEventToSlice` uses to route a sound to its dedicated channel if it has one and to the global mix otherwise.
- `Sum(otherMixer)` per-channel adds another mixer's tracks into this one with a SIMD-vectorized loop:

  ```csharp
  for (j = 0; j < chunked; j += chunkSize) {
      var existingVector = new Vector<float>(existingChannel.Slice(j, chunkSize));
      var incomingVector = new Vector<float>(incomingChannel.Slice(j, chunkSize));
      (existingVector + incomingVector).CopyTo(existingChannel.Slice(j, chunkSize));
  }
  ```

  This is used by the incremental render path.

### `BasicMixer` — the default mixdown strategy

```csharp
public class BasicMixer : IMixingMethod
{
    public AudioData<float> MixTracks((AudioLayout, AudioData<float>)[] tracks)
    {
        var export = AudioData<float>.WithLength(2, tracks[0].Item2.GetLength());
        foreach (var (layout, audio) in tracks)
            switch (layout) {
                case AudioLayout.AudioL:    BasicMix(audio.GetChannel(0), export.GetChannel(0)); break;
                case AudioLayout.AudioR:    BasicMix(audio.GetChannel(0), export.GetChannel(0)); break;
                case AudioLayout.AudioLr:   BasicMix(audio.GetChannel(0), export.GetChannel(0));
                                            BasicMix(audio.GetChannel(1), export.GetChannel(1)); break;
                case AudioLayout.AudioMono: BasicMix(audio.GetChannel(0), export.GetChannel(0));
                                            BasicMix(audio.GetChannel(0), export.GetChannel(1)); break;
            }
        return export;
    }
    private static void BasicMix(Memory<float> source, Memory<float> export) {
        for (int i = 0; i < source.Span.Length; i++) export.Span[i] += source.Span[i];
    }
}
```

A straight per-channel addition. There is no compression, limiting, or panning at the mixdown stage — pan is applied per-event in `RenderEventToSlice` and limiting is the user's job (or `EnableNormalization` at write time).

`IMixingMethod` is an interface so a future, smarter mixer (compressor, headroom-preserving sum, etc.) can be plugged in.

## Step C — Rendering placements into tracks

```csharp
public async Task RenderTimedEvents(AudioMixer mixer, TimedEvents events,
    Dictionary<(string, double), ProcessedEvent> processedEvents, int biggestEventLength,
    CancellationToken? cancellationToken = null)
{
    var channels = new Task[_channels];
    for (var i = 0; i < _channels; i++) {
        var index = i;
        channels[index] = Task.Run(async () =>
            await ProcessChannel(mixer, index, events, processedEvents, biggestEventLength));
    }
    await Task.WhenAll(channels);
}
```

There are **two layers of parallelism**:

1. **One `Task.Run` per audio channel** (1–2 tasks). Channels never interact during this phase, so they're embarrassingly parallel.
2. **Inside each channel**, the buffer is sliced into N chunks per `ChunkBoundaries` (below). Each chunk is processed independently via `Parallel.ForAsync`.

### `ChunkBoundaries` — the shared chunk grid

```csharp
private int[] ChunkBoundaries(int length)
{
    var min_length_per_thread = Math.Min(1 << 15, length);        // 32768
    var working_threads = _settings.MultithreadingSlices;         // default ProcessorCount × 4

    var min_length_for_working_threads = min_length_per_thread * working_threads;
    while (min_length_for_working_threads > length && working_threads > 1)
        min_length_for_working_threads = min_length_per_thread * --working_threads;

    var chunk_size = length / (float)working_threads;
    var boundaries = new int[working_threads + 1];
    for (var i = 0; i < working_threads; i++) boundaries[i] = Math.Min((int)(i * chunk_size), length);
    boundaries[working_threads] = length;

    return boundaries;
}
```

Thread count is **degraded gracefully**: short outputs use fewer slices so each chunk is at least 32768 samples (≈680ms at 48 kHz), avoiding overhead-dominated tiny chunks. This same grid is reused by the incremental path — `SnapToChunks` (below) grows a dirty range outward to these exact boundaries, because a boundary is observable in the output (`SampleMixer.HandleCut`'s silence search restarts at each chunk edge).

### `ProcessChannel`

```csharp
private async Task ProcessChannel(AudioMixer mixer, int channel, TimedEvents events,
    Dictionary<(string, double), ProcessedEvent> processedEvents, int biggestEventLength)
{
    var length = mixer.GetLength();
    var boundaries = ChunkBoundaries(length);

    await Parallel.ForAsync(1, boundaries.Length, (i, _) => {
        var start = boundaries[i - 1];
        var end   = boundaries[i];
        if (start >= end) return ValueTask.CompletedTask;
        ProcessChunk(start, end, mixer, channel, events, processedEvents, biggestEventLength);
        return ValueTask.CompletedTask;
    });
}
```

### `ProcessChunk` — placement filtering

```csharp
private void ProcessChunk(int start, int end, AudioMixer mixer, int channel,
    TimedEvents events, Dictionary<(string, double), ProcessedEvent> processedEvents, int biggestEventLength)
{
    var placement = events.Placement.AsSpan();

    foreach (var current in placement) {
        if (!current.Audible) continue;
        var current_start = (int)current.Index;

        // Skip placements that ended before this chunk:
        if (current_start < start - biggestEventLength) continue;
        // Stop early once placements move past this chunk:
        if (current_start >= end) break;

        RenderEventToSlice(start, end, mixer, channel, current, processedEvents);
    }
}
```

The early-exit `break` works because `Placement[]` is sorted by `Index` (`CalculateMany` sorts each sequence's slice). The `start - biggestEventLength` lower bound accounts for events that started in a *previous* chunk but whose tail leaks into this chunk.

Neither method takes an `invert` flag — the encoder no longer has a subtract-based render path (see "Incremental rendering" below); `ProcessChunk` is shared verbatim by both the full render and `RenderChunkRange`, the incremental path's partial re-render.

### `RenderEventToSlice` — the per-event renderer

This is where the actual audio "drawing" happens. Important branches:

#### 1. Resolve target track

```csharp
if (Holder.StringToSoundReferences.TryGetValue(event_name, out var sound_reference))
    event_name = sound_reference.Id;

var track_data = mixer.GetTrackOrDefault(event_name);   // dedicated track if exists, else default
var channel_data = track_data.GetChannel(channel).AsSpan();
var mix_slice = channel_data[start..end];
```

#### 2. `IndividualCutEvent` (#icut)

```csharp
case IndividualCutEvent individual_cut_event:
    foreach (var cut_track in individual_cut_event.CutSounds
             .Select(s => Holder.StringToSoundReferences.TryGetValue(s, out var r) ? r.Id : s)
             .Where(s  => mixer.HasTrack(s))
             .Select(s => mixer.GetTrack(s)))
    {
        SampleMixer.HandleCut(start, end, current_start, cut_track.GetChannel(channel).AsSpan()[start..end],
            _sampleRate, _settings.CutFadeLengthMs);
    }
    return;
```

Cuts only the listed tracks. Each track is faded then zeroed.

#### 3. `ExtendedEvent` (pan + offset)

```csharp
case ExtendedEvent extended_event:
    pan         = Math.Clamp(extended_event.Pan, -100f, 100f);
    startOffset = Math.Max(extended_event.OffsetInSeconds, 0);
```

These values are applied later when computing volume and the source slice.

#### 4. Global `!cut`

```csharp
if (event_name == "!cut") {
    foreach (var (_, data) in mixer.GetTracks())
        SampleMixer.HandleCut(start, end, current_start, data.GetChannel(channel).AsSpan()[start..end],
            _sampleRate, _settings.CutFadeLengthMs);
    return;
}
```

Cuts every track at this index.

#### 5. Look up the resampled sample

```csharp
if (!processedEvents.TryGetValue((event_name, event_value), out var processed_event)) {
    Log($"Event {event_name} with value {event_value} not found in processed events");
    return;
}
var current_length  = processed_event.AudioData.GetLength();
var current_channel = processed_event.AudioData.GetChannel(channel);
```

#### 6. Compute slice intersection

```csharp
var delta_start = current_start - start;
var delta_end   = current_length;
var offset      = 0;
if (delta_start < 0) { offset = -delta_start; delta_start = 0; }
delta_end -= offset;
```

This handles the two ways a placement can straddle a chunk boundary:

- The placement's *start* is before the chunk (some samples already played in a previous chunk → `offset` skips those).
- The placement's *end* is after the chunk (`RenderSample`'s length cap takes care of this).

#### 7. `OffsetInSeconds` (extended events only)

```csharp
if (startOffset > 0) {
    var event_sample_rate = _sampleRate / Math.Pow(2, event_value / 12);
    var offsetInSamples   = (int)(startOffset * event_sample_rate);
    if (offsetInSamples > current_length) offsetInSamples = current_length;
    offset += offsetInSamples;
    delta_end -= offsetInSamples;
}
```

The offset is in seconds at the *event's* sample rate (i.e., the rate it was resampled to during step A), so the same `2^(value/12)` math is applied to convert to samples.

#### 8. Pan attenuation

```csharp
switch (pan) {
    case < 0 when channel == 1: {                     // pan left → attenuate right channel
        var percent_subtract = 1f + pan / 100f;        // pan = -100 → 0 ; pan = 0 → 1
        volume *= _settings.PanScale switch {
            PercentageScale.Logarithmic                  => MathF.Sqrt(percent_subtract),
            PercentageScale.LinearOverflowLogarithmic
            or PercentageScale.Linear                    => percent_subtract,
            _                                            => 0
        };
        break;
    }
    case > 0 when channel == 0: {                     // pan right → attenuate left channel
        var percent_subtract = 1f - pan / 100f;
        volume *= _settings.PanScale switch { ... };
        break;
    }
}
```

The encoder doesn't *boost* the toward-side; it *attenuates the opposite side*. So a pan of `-100` zeroes the right channel entirely. `PanScale` controls whether the curve is linear or square-root — unless it's `EqualPower`, which is handled earlier by a separate `RenderEqualPowerPan` path (see below) instead of this switch.

#### `RenderEqualPowerPan` — the default pan mode

`PanScale` defaults to `PercentageScale.EqualPower`, so most renders never reach the switch above. Before it, `RenderEventToSlice` checks `pan != 0 && _settings.PanScale == PercentageScale.EqualPower` and, if true, delegates to `RenderEqualPowerPan` and returns — applying the same constant-power cosine/sine curve as the Web Audio API's `StereoPannerNode`, which the Thirty Dollar Website itself uses for panning. Mono and stereo sources are handled differently:

- **Mono source** — split into L/R using `gain = cos(angle)` (left) / `sin(angle)` (right), where `angle = (pan/100 + 1) * π/4`.
- **Stereo source** — instead of just attenuating the opposite channel, the far channel is downmixed *into* the panned-to side (a well-known `StereoPannerNode` quirk): the near channel gets unity gain (or `cos`/`sin` of a half-angle past center), and the encoder renders an extra `SampleMixer.RenderSample` call to mix the opposite channel's samples onto the near channel.

#### 9. Hand off to `SampleMixer.RenderSample`

```csharp
SampleMixer.RenderSample(current_channel, mix_slice, delta_start,
    volume, _settings.VolumeScale, delta_end, offset);
```

### `SampleMixer.RenderSample` — SIMD blend

`RenderSample` is a `static` method on `ThirtyDollarConverter.Encoder.Mixers.SampleMixer`, shared by every render path (full and incremental):

```csharp
public static void RenderSample(Span<float> source, Span<float> destination, int index,
    double volume, PercentageScale volumeScale, int length = -1, int offset = -1)
{
    if (length == -1) length = source.Length;
    if (offset < 0) offset = 0;

    var s_slice    = source.Slice(offset, length);
    var d_slice    = destination[index..];
    var chunk_size = Vector<float>.Count;

    var final_volume = (float)volume / 100f;
    switch (volumeScale) {
        case PercentageScale.Logarithmic:
        case PercentageScale.LinearOverflowLogarithmic when final_volume > 1f:
            final_volume = MathF.Sqrt(final_volume); break;
    }

    var s_chunked = s_slice.Length - s_slice.Length % chunk_size;
    var d_chunked = d_slice.Length - d_slice.Length % chunk_size;
    var min       = Math.Min(s_chunked, d_chunked);

    for (var i = 0; i < min; i += chunk_size) {
        var s_vector = new Vector<float>(s_slice[i..(i+chunk_size)]);
        var d_vector = new Vector<float>(d_slice[i..(i+chunk_size)]);
        var final    = d_vector + s_vector * final_volume;
        final.CopyTo(d_slice[i..(i+chunk_size)]);
    }

    // ...scalar tail...
}
```

The hot loop uses `System.Numerics.Vector<float>` so each iteration processes `Vector<float>.Count` floats (typically 4, 8, or 16 depending on the CPU's SIMD width). The remaining tail is handled by a scalar loop. `RenderSample` always *adds* into the destination — there is no subtract/invert mode; the incremental render path (below) gets subtraction-free updates by clearing and re-rendering a dirty chunk range instead.

`VolumeScale` here mirrors `PanScale` semantics:

- `Linear` → `final_volume = volume / 100`
- `LinearOverflowLogarithmic` → linear up to 1.0, `sqrt` above
- `Logarithmic` → always `sqrt(volume / 100)`

### `SampleMixer.HandleCut` — the cut fade

`HandleCut` is likewise `static` on `SampleMixer`, taking the sample rate and fade length explicitly instead of reading `_settings`:

```csharp
public static void HandleCut(int start, int end, int currentStart, Span<float> mixSlice,
    uint sampleRate, uint cutFadeLengthMs)
{
    var wanted_zero_samples = 4096 * sampleRate / 48000;          // ≈ 4096 @ 48k, scaled per rate
    var norm_start = currentStart - start;
    var norm_end   = end - start;

    // 1. find a "silence index" — extend the cut up to the next sample whose absolute value is 0
    var zero_samples = 0;
    var zero_index   = norm_end;
    for (var i = norm_start; i < norm_end; i++) {
        if (zero_samples >= wanted_zero_samples) { zero_index = i; break; }
        zero_samples++;
        if (i >= 0 && mixSlice[i] == 0f) continue;
        zero_samples = 0;
    }

    // 2. apply a linear fade from 1.0 → 0.0 over cutFadeLengthMs
    var cut_fade_ms     = (int)cutFadeLengthMs;
    var cut_fade_length = (int)(sampleRate / 1000) * cut_fade_ms;
    var cut_fade_end    = norm_start + cut_fade_length;
    int cut_i;
    for (cut_i = norm_start; cut_i < cut_fade_end; cut_i++) {
        if (cut_i < 0 || cut_i >= zero_index) continue;
        var norm_i = cut_fade_end - cut_i;
        var delta  = (float)norm_i / cut_fade_length;
        mixSlice[cut_i] *= delta;
    }

    // 3. zero everything between the fade-end and the silence index
    for (var i = cut_i; i < zero_index; i++) {
        if (i < 0) continue;
        mixSlice[i] = 0f;
    }
}
```

The fade is `CutFadeLengthMs` long (default 4ms = 192 samples at 48kHz) and the cut extends until either (a) the next zero crossing within ~4096 samples (~85ms at 48k), or (b) that 4096-sample budget runs out. Hard zero after that. This avoids the click artifact from instantaneously zeroing a non-zero waveform.

## Step D — Mixdown and write

```csharp
return (mixer.MixDown(), mixer);
```

`MixDown()` invokes `BasicMixer.MixTracks` to sum every track into a single `AudioData<float>`. This is what gets stored in `RenderedSequence.Audio`.

### `WriteAsWavFile`

`PcmEncoder.WriteAsWavFile` (both the `string location` and `Stream` overloads) just delegates to `WaveEncoder.WriteAsWavFloat32File` in `ThirtyDollarConverter.Encoder/Wave/WaveEncoder.cs` — the actual write logic lives there, not on the encoder:

```csharp
public void WriteAsWavFile(Stream stream, AudioData<float> data)
{
    WaveEncoder.WriteAsWavFloat32File(stream, data, _channels, _sampleRate, _settings.EnableNormalization,
        IndexReport);
    Log("Saved audio file.");
}
```

`WaveEncoder.WriteAsWavFloat32File`:

1. Optionally normalizes (`data.Normalize()`, divides every sample by the absolute maximum).
2. Computes each channel's trimmed length up front (`TrimmedLength` — the count of samples before trailing zeros), rather than mutating the arrays.
3. Writes a RIFF WAVE header via `AddWavHeader<float>` (always 32-bit float — `audioFormat = 3`), sized to the *longest* trimmed channel.
4. **Interleaves** the planar `T[channel][sample]` data back to LRLR…LR while writing, through a pooled (`ArrayPool<byte>`) ~64 KiB byte buffer flushed to the stream in frame-aligned chunks — samples past a channel's trimmed length are written as `0f`.
5. Reports progress roughly 200 times across the whole write, plus a guaranteed terminal 100% call.

The header reflects whatever `sampleRate` and `channels` were passed in — for `PcmEncoder.WriteAsWavFile` specifically, that's `_sampleRate`/`_channels` from `EncoderSettings`.

## Incremental rendering — `ComputeIncrementalAudio`

Editing a sequence and re-rendering from scratch wastes work. The encoder caches `Mixer` and `ProcessedEvents` on the `RenderedSequence`, then offers:

```csharp
public async Task<RenderedSequence> ComputeIncrementalAudio(RenderedSequence old, IEnumerable<Sequence> new);
```

This replaced an earlier "subtract-based" design that rendered removed placements into an overlay mixer with an `invert` flag and subtracted it from the base mix — that approach needed the *old* processed samples to subtract with, which conflicted with pruning them early. The current algorithm instead clears and fully re-renders only the chunk range an edit can be heard in:

1. Recompute placements for the new sequence(s).
2. Compute the multiset difference between old and new placements (`MultisetDifference`, backed by `Placement.Equals` — which compares sound name, value, volume, pan, offset, index, audible, and cut-sound set):
   - `to_remove` = old ∖ new
   - `to_add`    = new ∖ old
   - If neither changed, return the old `RenderedSequence` unchanged.
3. **Fall back to a full render (`GetMultipleSequencesAudio`)** if the old mixer/processed-events are missing, if a new sequence references a track the old mixer never allocated, or if the total rendered length changed.
4. Resample only the new/changed events (`GetAudioSamples`, reusing the old processed-event dictionary as a cache), then prune samples no longer referenced (`RemoveUnusedAudioSamples`) — done *before* measuring length, since the new algorithm doesn't need the old samples for a subtraction step.
5. Compute the sample range spanning every changed placement (`to_remove.Concat(to_add)`), then snap it outward to whole chunk boundaries (`SnapToChunks`) — partial-chunk `HandleCut` calls would zero a different number of samples than a full render, so re-rendering has to stay chunk-aligned to keep output bit-identical to a from-scratch render.
6. Clear that sample range on every track, then re-render only that range (`RenderChunkRange`) using the full new placement list — not just the changed ones, since untouched placements inside the dirty range still need to be redrawn into the now-empty slice.
7. Re-mix down → `RenderedSequence.Audio`.

## Threading summary

```text
GetMultipleSequencesAudio
└─ Parallel.ForEachAsync   (over unique events)              ← sample resample
   │                                                           uses _indexLock for progress
   └─ SampleProcessor.ProcessEvent → IResampler.Resample

GenerateAudioAndMixer
└─ RenderTimedEvents
   ├─ Task.Run × _channels (1 or 2)                          ← per-channel
   │   └─ Parallel.ForAsync × MultithreadingSlices           ← per-chunk inside channel
   │       └─ ProcessChunk
   │           └─ RenderEventToSlice
   │               └─ RenderSample (SIMD Vector<float>)
   └─ await Task.WhenAll(channels)
└─ AudioMixer.MixDown                                        ← BasicMixer
```

So at peak the encoder uses up to `_channels × MultithreadingSlices` chunk-level threads (e.g., `2 × ProcessorCount × 4`), plus the runtime's `Parallel.ForEachAsync` pool for the resample step. The progress callbacks are serialized by `_indexLock` so the UI never sees racy updates.

---

**Previous:** [Calculating the Placement](4%20-%20Calculating%20the%20Placement.md)
**Up:** [Converter](../Converter.md)
