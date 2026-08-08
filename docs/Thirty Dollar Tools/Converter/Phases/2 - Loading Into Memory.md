# Phase 2 — Loading Into Memory

> Owning code: `SampleHolder.LoadSamplesIntoMemory()`, `WaveDecoder` (in `ThirtyDollarConverter.Audio/Wave/`), `PcmDataHolder`, `AudioData<T>`, `DataHolderExtensions`.

After [Getting All Samples](1%20-%20Getting%20All%20Samples.md) has guaranteed every sound exists on disk, Phase 2 reads each `.wav` file, decodes its RIFF/WAVE container, and parks the result in a `PcmDataHolder` keyed by `Sound`. It also walks the samples directory a second time to pick up any **custom** `.wav` files the user dropped in, registering them as new sounds.

This is purely an in-memory load — no resampling happens here. Resampling is per-event and lazy, and it lives in [Encoding](5%20-%20Encoding.md).

## `SampleHolder.LoadSamplesIntoMemory()`

```csharp
public void LoadSamplesIntoMemory()
{
    Parallel.ForEach(SampleList, r => {
        var key = r.Key;
        var file_stream = File.OpenRead($"{SamplesLocation}{Slash}{key.Id}.wav");
        var decoder = new WaveDecoder();
        lock (SampleList) {
            SampleList[key] = decoder.Read(file_stream);
            SampleList[key].ReadAsFloat32Array(true);   // pre-warm Float32 cache
        }
    });

    // Pick up CUSTOM .wav files (anything in Sounds/ that isn't a known TDW sound)
    var added_hash_set = SampleList.Select(r => r.Key.Filename ?? "").ToHashSet();
    Parallel.ForEach(Directory.GetFiles(SamplesLocation), file => {
        var filename = file.Split(Slash).Last();
        if (!filename.EndsWith(".wav")) return;
        var sound = filename.Replace(".wav", "");
        if (added_hash_set.Contains(sound)) return;

        var sound_object = new Sound { Id = sound, Name = sound };
        var holder = new WaveDecoder().Read(File.OpenRead(...));
        holder.ReadAsFloat32Array(true);
        lock (SampleList) { SampleList.Add(sound_object, holder); }
    });
}
```

Key behaviors:

- **Two parallel passes.**
  1. First, every known TDW sound is decoded in parallel via `Parallel.ForEach`.
  2. Then `Directory.GetFiles(SamplesLocation)` is walked again to find any extra `.wav` files (custom user samples) that weren't in `sounds.json`. They get registered with a synthetic `Sound { Id = filename, Name = filename }`.
- **Lock-protected dictionary writes.** `SampleList` is a `Dictionary<,>` (not concurrent), so the parallel loops `lock (SampleList)` for every assignment.
- **Pre-warmed Float32 cache.** Right after decoding, `ReadAsFloat32Array(true)` is called. `true` here forces mono → stereo duplication so downstream stages can always assume two channels. The result is cached on the holder, so when [Encoding](5%20-%20Encoding.md) later asks for the audio it's just a property read.

The end-state is: every `Sound` key in `SampleList` maps to a fully populated `PcmDataHolder` with `FloatData` already converted.

## `WaveDecoder` — the RIFF/WAVE reader

> File: `ThirtyDollarConverter.Audio/Wave/WaveDecoder.cs`
> The header comment cheerfully admits "Shamefully copied from NAudio. Here goes copyright infringement." It is a minimal, single-pass decoder.

`WaveDecoder.Read(Stream input)` returns a `PcmDataHolder`. It performs:

1. **`ReadRiffHeader(reader)`** — looks at the first 4 bytes:
   - `RIFF` → standard 32-bit RIFF (returns `1`)
   - `RF64` → 64-bit RIFF (returns `2`, and reads a follow-up `ds64` chunk for the real lengths)
   - anything else → throws.
2. **Reads the 32-bit `riffFileSize`** field (overwritten by the `ds64` chunk if present).
3. **Asserts the next 4 bytes are `WAVE`**, otherwise throws `FileLoadException`.
4. **Walks chunks** until it finds either `fmt ` or `data`:

   | Chunk ID | Action |
   | --- | --- |
   | `fmt ` | `ReadWaveFormat()` — parses channel count, sample rate, encoding (`Encoding` enum). Warns to stdout if `waveFormatTag != 1` (non-int PCM). |
   | `data` | Records `_dataChunkLength` and breaks the walk. |
   | other  | Skipped via `inputStream.Position += chunkLength`. |

5. **Bulk-reads `_dataChunkLength` bytes** into `_holder.AudioData` (the raw byte buffer) and computes:

   ```csharp
   _holder.Samples = (uint)(bytes.Length / _holder.Channels / (int)_holder.Encoding * 4);
   ```

That's it — no decoding into floats happens here. The bytes are stored as raw PCM and the holder records its `SampleRate`, `Channels`, and `Encoding`. Decoding happens later, lazily, in `DataHolderExtensions`.

## `PcmDataHolder` — the raw container

```csharp
public class PcmDataHolder
{
    public readonly SemaphoreSlim Semaphore = new(1);
    public AudioData<float>? FloatData = null;
    public AudioData<short>? ShortData = null;
    public uint SampleRate  { get; set; }
    public uint Channels    { get; set; }
    public uint Samples     { get; set; }
    public Encoding Encoding { get; set; }
    public byte[]? AudioData { get; set; }       // raw PCM bytes from the WAV
    public AdditionalData? AdditionalData { get; set; } = null;
}
```

It holds:

- The **raw PCM bytes** straight from the WAV.
- Two optional **decoded caches**: `FloatData` (`AudioData<float>`) and `ShortData` (`AudioData<short>`). These are populated lazily by `ReadAsFloat32Array` / `ReadAsInt16Array` and reused on subsequent calls.
- A `SemaphoreSlim` to make the lazy decode thread-safe (the encoder is heavily parallel).

## `Encoding` — supported PCM bit depths

```csharp
public enum Encoding
{
    Int8    = 8,
    Int16   = 16,
    Int24   = 24,    // backed by Int24 struct (3 packed bytes, LE)
    Float32 = 32
}
```

The numeric values are deliberately the bit-depth — they double as "size in bits" later when the holder calculates its sample count.

`Int24` is hand-rolled (`Converter/ThirtyDollarConverter.Audio/PCM/Int24.cs`) as a `[StructLayout(LayoutKind.Sequential, Pack=1)]` struct of three bytes so that `MemoryMarshal.Cast<byte, Int24>` round-trips without copy. `Int24Extensions` provides `.ToFloat()` for normalization to `[-1, 1]`.

## `AudioData<T>` — the per-channel container

```csharp
public class AudioData<T> : IDisposable
    where T : INumber<T>, IComparable<T>, IEquatable<T>,
              IMultiplyOperators<T,T,T>, IDivisionOperators<T,T,T>
{
    public readonly uint ChannelCount;
    public T[][] Samples;            // Samples[channel][sample_index]
    ...
    public static AudioData<float> Empty(uint channelCount);
    public static AudioData<float> WithLength(uint channels, int length);

    public T[] GetChannel(int index);
    public void Normalize();
    public int  GetLength();
    public void Dispose();
}
```

The shape is `T[channelCount][sampleCount]`. Crucially, **channels are separate arrays** (planar), not interleaved. This:

- makes a single channel a contiguous `Span<T>` for SIMD operations in [Encoding](5%20-%20Encoding.md),
- lets parallel workers operate on different channels without tripping on each other,
- mirrors the layout `AudioMixer.Sum()` and `PCMEncoder.RenderSample()` consume directly.

`Normalize()` divides every sample by the absolute maximum found across all channels (used by `WriteAsWavFile` when `EncoderSettings.EnableNormalization` is on).

`AudioLayout` is an enum that decorates an `AudioData<float>` track inside an `AudioMixer` to describe how its channels should be interpreted (`AudioL`, `AudioR`, `AudioMono`, `AudioLr`). It's only really exercised on the mixer side; samples coming out of `WaveDecoder` always land as `AudioLr` (or mono-duplicated to `AudioLr` if `monoToStereo: true`).

## `DataHolderExtensions` — the lazy decode

`PcmDataHolder.AudioData` (raw bytes) is converted to `AudioData<float>` (planar floats in `[-1, 1]`) on demand via `holder.ReadAsFloat32Array(monoToStereo)`. The implementation:

1. Acquires `holder.Semaphore` (per-holder lock).
2. **Returns the cache** (`holder.FloatData`) if already decoded.
3. Uses `MemoryMarshal.Cast<byte, T>` to build zero-copy "views" of the raw bytes as `short[]`, `Int24[]`, and `float[]` simultaneously.
4. Allocates output channel arrays — duplicating mono → stereo when `monoToStereo` is `true`.
5. For each output channel, walks the interleaved source and writes into the planar destination, normalizing per-encoding:

| Source `Encoding` | Conversion to `float` |
| --- | --- |
| `Int8`     | `byte / 256f` |
| `Int16`    | `short / 32768f` |
| `Int24`    | `Int24.ToFloat()` (range-normalized) |
| `Float32`  | passthrough |

The `ReadAsInt16Array` variant is symmetrical but yields `AudioData<short>` and is used in places that want to write 16-bit WAV without the float→short rounding.

After this method returns, `holder.FloatData` is non-null and every subsequent call short-circuits to that cached object.

## Relationship to `SampleProcessor`

`Phase 2` produces *file-rate* audio — the original samples at, say, 48 kHz, in their natural pitch. It does **not** apply the `Value` (semitone) transposition of an event yet. That happens per-event in `SampleProcessor.ProcessEvent()`, which passes `holder.ReadAsFloat32Array(...)` through an `IResampler` whose target rate depends on the event's `Value` and the encoder's `SampleRate`. See [Encoding](5%20-%20Encoding.md) for the math.

---

**Previous:** [Getting All Samples](1%20-%20Getting%20All%20Samples.md)
**Next:** [Parsing Sequences](3%20-%20Parsing%20Sequences.md)
**Up:** [Converter](../Converter.md)
