# Converter

The **Converter** is the part of the project responsible for turning a Thirty Dollar Website (TDW) sequence (a text format describing a "cover" — sound events, timing actions, jumps, etc.) into a fully rendered piece of audio (a `.wav` file or, downstream, an `.ogg` / `.mp3`).

Conceptually it is one library (`ThirtyDollarConverter`) plus a few support libraries (`ThirtyDollarConverter.Parser`, `ThirtyDollarConverter.Audio`) that together implement the encoding pipeline, surrounded by three thin front-end applications (CLI, GUI, Discord Bot) that just feed sequences into the pipeline.

## The Pipeline (TL;DR)

```
.🗿 / TDW text
       │
       │  Sequence.FromString()           Parsing Sequences
       ▼
   Sequence (BaseEvent[])
       │
       │  PlacementCalculator             Calculating the Placement
       ▼
   Placement[]   (sample-index timeline)
       │
       │  SampleProcessor + IResampler    Loading Into Memory
       ▼              ↑
   ProcessedEvent[]   │ raw samples come from
       │              │ SampleHolder       Getting All Samples
       │
       │  PCMEncoder (channels × chunks)  Encoding
       ▼
   AudioMixer → AudioData<float>
       │
       │  WriteAsWavFile()
       ▼
   RIFF WAVE file
```

Each block of the pipeline has its own page in **Phases/**, and they read in the order shown above.

## Phases

The five phases of the encoder, in execution order:

1. [Getting All Samples](Phases/1%20-%20Getting%20All%20Samples.md) — Loading `sounds.json`, downloading every sound, and discovering custom user samples. Owned by `SampleHolder`.
2. [Loading Into Memory](Phases/2%20-%20Loading%20Into%20Memory.md) — Decoding every `.wav` from disk into `PcmDataHolder` / `AudioData<float>`. Covers `WaveDecoder`.
3. [Parsing Sequences](Phases/3%20-%20Parsing%20Sequences.md) — Turning the raw `|`-delimited text into a `Sequence` of `BaseEvent`s, including `#define` macros, `#icut`, `!bg`, `!pulse`, etc.
4. [Calculating the Placement](Phases/4%20-%20Calculating%20the%20Placement.md) — Walking the sequence as a tiny VM (BPM, transpose, volume, jumps, loops, cuts) to produce a flat `Placement[]` indexed in audio samples.
5. [Encoding](Phases/5%20-%20Encoding.md) — Resampling each unique sound, then mixing every placement into the final stereo buffer using SIMD-vectorized chunks across multiple worker threads. Covers `PCMEncoder`, `AudioMixer`, `ProcessedEvent`, `RenderedSequence`, `TimedEvent`.

## Front-ends

Each of these is a thin shell around the same `PcmEncoder` library:

- [Converter.CLI](Converter.CLI/Converter.CLI.md) — `dotnet run`, `-i input.🗿 -o output.wav`. Runs encoder, writes file.
- [Converter.GUI](Converter.GUI/Converter.GUI.md) — Avalonia + ReactiveUI desktop app. Provides a settings window, progress bar, log view.
- [Discord Bot](Discord%20Bot/Discord%20Bot.md) — DSharpPlus slash bot. Right-click a TDW message attachment → "TDW to OGG/MP3", server pipes encoder output through `ffmpeg`.

> `ThirtyDollarConverter.Next/` is an in-progress redesign of the encoder and is intentionally **not** documented here.

## Project layout

```
Converter/
├── ThirtyDollarConverter/                  (core encoder library)
│   ├── PCMEncoder.cs                       ← the orchestrator
│   ├── PlacementCalculator.cs              ← sequence → timeline
│   ├── SampleProcessor.cs                  ← per-event resampling
│   ├── SampleHolder.cs                     ← sample download/load
│   ├── ProcessedEvent.cs                   ← resampled audio container
│   ├── EventType.cs
│   └── Objects/
│       ├── EncoderSettings.cs
│       ├── Placement.cs
│       ├── RenderedSequence.cs
│       └── TimedEvents.cs
│
├── ThirtyDollarConverter.Parser/           (sequence text → events)
│   ├── Sequence.cs                         ← FromString() lives here
│   ├── BaseEvent.cs / NormalEvent.cs
│   ├── Sound.cs / ValueScale.cs
│   └── Custom Events/
│       ├── ExtendedEvent.cs                (+ pan, + offset)
│       ├── IndividualCutEvent.cs           (#icut)
│       ├── BookmarkEvent.cs / EndEvent.cs / LegacySequenceEvent.cs
│       └── ICustomActionEvent / ICustomAudibleEvent / IHiddenEvent
│
├── ThirtyDollarConverter.Encoder/          (PCM containers, mixers, resamplers)
│   ├── PCM/
│   │   ├── AudioData.cs                    ← per-channel float[][] container
│   │   ├── AudioMixer.cs                   ← multi-track mixer
│   │   ├── PcmDataHolder.cs                ← raw decoded WAV
│   │   ├── PercentageScale.cs              ← Linear / LinearOverflowLogarithmic / Logarithmic / EqualPower
│   │   ├── AudioLayout.cs / Encoding.cs / Int24.cs
│   │   └── DataHolderExtensions.cs / Int24Extensions.cs
│   ├── Mixers/
│   │   ├── IMixingMethod.cs
│   │   ├── SampleMixer.cs                  (static RenderSample / HandleCut helpers used by PCMEncoder)
│   │   └── BasicMixer.cs                   (current default — straight sum)
│   ├── Resamplers/
│   │   ├── IResampler.cs
│   │   ├── HannSincResampler.cs            (default in core)
│   │   ├── HermiteResampler.cs             (default in CLI)
│   │   ├── KaiserBest / KaiserFast / KaiserSinc
│   │   ├── LinearResampler.cs / NoInterpolationResampler.cs
│   │   └── ByteCruncherResampler.cs
│   └── Wave/
│       ├── WaveDecoder.cs                  (RIFF / RF64 reader)
│       └── WaveEncoder.cs
│
├── ThirtyDollarConverter.Editor/           (project-file model: Note, TrackAutomation, SequenceBuilder — undocumented)
│
├── ThirtyDollarConverter.CLI/              → Converter.CLI
├── ThirtyDollarConverter.GUI/              → Converter.GUI
├── ThirtyDollarConverter.DiscordBot/       → Discord Bot
├── ThirtyDollarConverter.Tests/            (xUnit)
├── ThirtyDollarConverter.Editor.Tests/     (xUnit, for ThirtyDollarConverter.Editor)
└── ThirtyDollarConverter.Benchmarks/       (BenchmarkDotNet — encoder + editor workflows)
```

## Recommended reading order

If you are new to the project, read the phases linearly:

1. [Getting All Samples](Phases/1%20-%20Getting%20All%20Samples.md)
2. [Loading Into Memory](Phases/2%20-%20Loading%20Into%20Memory.md)
3. [Parsing Sequences](Phases/3%20-%20Parsing%20Sequences.md)
4. [Calculating the Placement](Phases/4%20-%20Calculating%20the%20Placement.md)
5. [Encoding](Phases/5%20-%20Encoding.md)

Then skim whichever front-end you actually plan to use ([CLI](Converter.CLI/Converter.CLI.md), [GUI](Converter.GUI/Converter.GUI.md) or [Bot](Discord%20Bot/Discord%20Bot.md)).
