# Phase 5 — Encoding

> Owning code: `ThirtyDollarConverter/PCMEncoder.cs`, `SampleProcessor.cs`, `ProcessedEvent.cs`, `Objects/RenderedSequence.cs`, `Objects/EncoderSettings.cs`, `Objects/PercentageScale.cs`, plus `ThirtyDollarConverter.Audio/PCM/AudioMixer.cs` and the resampler library.

This is the heaviest phase. Given a `TimedEvents` (the output of [[4 - Calculating the Placement|Calculating the Placement]]) and the in-memory `SampleHolder` (from [[2 - Loading Into Memory|Loading Into Memory]]), the encoder produces a fully-mixed stereo `AudioData<float>` and optionally writes it as a `.wav` file.

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
    public IResampler Resampler        = new HannSincResampler();
    public PercentageScale VolumeScale = PercentageScale.LinearOverflowLogarithmic;
    public PercentageScale PanScale    = PercentageScale.Linear;
    public bool    AddVisualEvents;
    public string  DownloadLocation { get; set; } = "";
}
```

The constructor of `PcmEncoder` rejects `Channels < 1` and `Channels > 2` (anything other than mono / stereo throws). All other settings are runtime-configurable.

`PercentageScale` controls how a percentage (volume / pan) is mapped to a multiplier:

```csharp
public enum PercentageScale { Linear, LinearOverflowLogarithmic, Logarithmic }
```

| Value | Multiplier formula |
| --- | --- |
| `Linear` | `multiplier = pct / 100` (1:1) |
| `LinearOverflowLogarithmic` | linear up to 100%, `sqrt(pct/100)` past 100% |
| `Logarithmic` | always `sqrt(pct/100)` |

Used both in `RenderSample` (volume) and in pan attenuation.

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

1. Asks the `PlacementCalculator` for a sorted `Placement[]` (this is [[4 - Calculating the Placement|Calculating the Placement]]).
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

   Both `Mixer` and `ProcessedEvents` are kept around so a subsequent edit can be incrementally re-rendered (see [[#Incremental rendering — `ComputeIncrementalAudio`|the incremental render section]]).

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

Then it iterates `sequence.SeparatedChannels` (populated by [[3 - Parsing Sequences|Parsing Sequences]] from `#icut` and `!cut@…`) and adds one **track per separated sound** to the mixer:

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
    Dictionary<(string, double), ProcessedEvent> processedEvents,
    int biggestEventLength, bool invert = false, ...)
{
    var channels = new Task[_channels];
    for (var i = 0; i < _channels; i++) {
        var index = i;
        channels[index] = Task.Run(async () =>
            await ProcessChannel(mixer, index, events, processedEvents, biggestEventLength, invert));
    }
    await Task.WhenAll(channels);
}
```

There are **two layers of parallelism**:

1. **One `Task.Run` per audio channel** (1–2 tasks). Channels never interact during this phase, so they're embarrassingly parallel.
2. **Inside each channel**, the buffer is sliced into N chunks where N = `MultithreadingSlices` (default `ProcessorCount × 4`). Each chunk is processed independently via `Parallel.ForAsync`.

### `ProcessChannel`

```csharp
private async Task ProcessChannel(AudioMixer mixer, int channel, TimedEvents events,
    Dictionary<(string, double), ProcessedEvent> processedEvents, int biggestEventLength, bool invert = false)
{
    var length                    = mixer.GetLength();
    var min_length_per_thread     = Math.Min(1 << 15, length);   // 32768
    var working_threads           = _settings.MultithreadingSlices;
    var min_length_for_working_threads = min_length_per_thread * working_threads;
    while (min_length_for_working_threads > length && working_threads > 1)
        min_length_for_working_threads = min_length_per_thread * --working_threads;

    var chunk_size = length / (float)working_threads;

    await Parallel.ForAsync(1, working_threads + 1, (i, _) => {
        var start = (int)((i - 1) * chunk_size);
        var end   = Math.Min((int)(i * chunk_size), length);
        if (start > length) return ValueTask.CompletedTask;
        ProcessChunk(start, end, mixer, channel, events, processedEvents, biggestEventLength, invert);
        return ValueTask.CompletedTask;
    });
}
```

The thread count is **degraded gracefully**: short outputs use fewer slices so each chunk is at least 32768 samples (≈680ms at 48 kHz), avoiding overhead-dominated tiny chunks.

### `ProcessChunk` — placement filtering

```csharp
private void ProcessChunk(int start, int end, AudioMixer mixer, int channel, ...)
{
    var placement = events.Placement.AsSpan();

    foreach (var current in placement) {
        if (!current.Audible) continue;
        var current_start = (int)current.Index;

        // Skip placements that ended before this chunk:
        if (current_start < start - biggestEventLength) continue;
        // Stop early once placements move past this chunk:
        if (current_start >= end) break;

        RenderEventToSlice(start, end, mixer, channel, current, processedEvents, invert);
    }
}
```

The early-exit `break` works because `Placement[]` is sorted by `Index` (`CalculateMany` sorts each sequence's slice). The `start - biggestEventLength` lower bound accounts for events that started in a *previous* chunk but whose tail leaks into this chunk.

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
        HandleCut(start, end, current_start, cut_track.GetChannel(channel).AsSpan()[start..end]);
    }
    return;
```

Cuts only the listed tracks. Each track is faded then zeroed.

#### 3. `ExtendedEvent` (pan + offset)

```csharp
case ExtendedEvent extended_event:
    pan         = Math.Clamp(extended_event.Pan, -1f, 1f);
    startOffset = Math.Max(extended_event.OffsetInSeconds, 0);
```

These values are applied later when computing volume and the source slice.

#### 4. Global `!cut`

```csharp
if (event_name == "!cut") {
    foreach (var (_, data) in mixer.GetTracks())
        HandleCut(start, end, current_start, data.GetChannel(channel).AsSpan()[start..end]);
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
        var p_sub = 1f + pan;                         // pan = -1 → 0 ; pan = 0 → 1
        volume *= _settings.PanScale switch {
            PercentageScale.Logarithmic                  => MathF.Sqrt(p_sub),
            PercentageScale.LinearOverflowLogarithmic
            or PercentageScale.Linear                    => p_sub,
            _                                            => 0
        };
        break;
    }
    case > 0 when channel == 0: {                     // pan right → attenuate left channel
        var p_sub = 1f - pan;
        volume *= _settings.PanScale switch { ... };
        break;
    }
}
```

The encoder doesn't *boost* the toward-side; it *attenuates the opposite side*. So a pan of `-1` zeroes the right channel entirely. `PanScale` controls whether the curve is linear or square-root.

#### 9. Hand off to `RenderSample`

```csharp
RenderSample(current_channel, mix_slice, delta_start,
             volume, _settings.VolumeScale, delta_end, offset, invert);
```

### `RenderSample` — SIMD blend

```csharp
public static void RenderSample(Span<float> source, Span<float> destination, int index,
    double volume, PercentageScale volumeScale, int length = -1, int offset = -1, bool invert = false)
{
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
        var src      = s_vector * final_volume;
        var final    = invert ? d_vector - src : d_vector + src;
        final.CopyTo(d_slice[i..(i+chunk_size)]);
    }

    // ...scalar tail...
}
```

The hot loop uses `System.Numerics.Vector<float>` so each iteration processes `Vector<float>.Count` floats (typically 4, 8, or 16 depending on the CPU's SIMD width). The remaining tail is handled by a scalar loop.

`invert = true` is used by the incremental render path (next section) to *subtract* a previously-mixed event before adding the replacement.

`VolumeScale` here mirrors `PanScale` semantics:

- `Linear` → `final_volume = volume / 100`
- `LinearOverflowLogarithmic` → linear up to 1.0, `sqrt` above
- `Logarithmic` → always `sqrt(volume / 100)`

### `HandleCut` — the cut fade

```csharp
private void HandleCut(int start, int end, int currentStart, Span<float> mixSlice)
{
    var wanted_zero_samples = 4096 * _sampleRate / 48000;        // ≈ 4096 @ 48k, scaled per rate
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

    // 2. apply a linear fade from 1.0 → 0.0 over CutFadeLengthMs
    var cut_fade_ms     = (int)_settings.CutFadeLengthMs;
    var cut_fade_length = (int)(_settings.SampleRate / 1000) * cut_fade_ms;
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

```csharp
public void WriteAsWavFile(Stream stream, AudioData<float> data)
{
    if (_settings.EnableNormalization) data.Normalize();

    var samples   = data.Samples;
    for (int i = 0; i < samples.Length; i++)
        samples[i] = samples[i].TrimEnd();   // trim silence padding

    var writer    = new BinaryWriter(stream);
    var maxLength = samples.Max(r => r.Length);
    AddWavHeader<float>(writer, maxLength);

    for (int i = 0; i < maxLength; i++) {
        if (i % every_n_report == 0) IndexReport((ulong)i, (ulong)maxLength);
        for (int j = 0; j < _channels; j++)
            writer.Write(samples[j].Length > i ? samples[j][i] : 0f);
    }
}
```

The writer:

1. Optionally normalizes (divides every sample by the absolute maximum).
2. Trims trailing silence per channel via `ObjectExtensions.TrimEnd`.
3. Writes a RIFF WAVE header (always 32-bit float — `audioFormat = 3`) using `AddWavHeader<float>`.
4. **Interleaves** the planar `T[channel][sample]` data back to LRLR…LR while writing.
5. Reports progress 200 times across the whole write.

The header reflects whatever `_settings.SampleRate` and `_channels` were configured with.

## Incremental rendering — `ComputeIncrementalAudio`

Editing a sequence and re-rendering from scratch wastes work. The encoder caches `Mixer` and `ProcessedEvents` on the `RenderedSequence`, then offers:

```csharp
public async Task<RenderedSequence> ComputeIncrementalAudio(RenderedSequence old, IEnumerable<Sequence> new);
```

The algorithm:

1. Recompute placements for the new sequence(s).
2. Compute set difference using `PlacementEqualityComparer` (placement equality compares name + value + volume + pan + offset + index + audible — same thing the user-visible output depends on):
   - `to_remove` = old ∖ new
   - `to_add`    = new ∖ old
3. **If any `!cut` is in either set, fall back to a full render.** Cuts can't be cleanly inverted because they zero data the encoder no longer has.
4. Resample only the new keys (step A with the old dictionary as the cache).
5. Render `to_remove ∪ cuts` into an overlay mixer with `invert = true` (so `RenderSample` *subtracts* them from the base mix).
6. Render `to_add ∪ cuts` into another overlay with `invert = false`.
7. Sum the larger mixer with the smaller via `AudioMixer.Sum` (SIMD).
8. Re-mix down → `RenderedSequence.Audio`.
9. Drop unused samples from the dictionary (`RemoveUnusedAudioSamples`).

The "cuts in both sets" trick is needed because cut events affect their slice every render — they have to be re-applied even when otherwise unchanged.

`PlacementEqualityComparer` is a private nested class in `PCMEncoder` that delegates to `Placement.Equals`.

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

**Previous:** [[4 - Calculating the Placement|Calculating the Placement]]
**Up:** [[../Converter|Converter]]
