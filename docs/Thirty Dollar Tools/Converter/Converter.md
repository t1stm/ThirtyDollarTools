# Converter

The **Converter** is the part of the project responsible for turning a Thirty Dollar Website (TDW) sequence (a text format describing a "cover" — sound events, timing actions, jumps, etc.) into a fully rendered piece of audio (a `.wav` file or, downstream, an `.ogg` / `.mp3`).

Conceptually it is one library (`ThirtyDollarConverter`) plus a few support libraries (`ThirtyDollarConverter.Parser`, `ThirtyDollarConverter.Audio`) that together implement the encoding pipeline, surrounded by three thin front-end applications (CLI, GUI, Discord Bot) that just feed sequences into the pipeline.

## The Pipeline (TL;DR)

```
.🗿 / TDW text
       │
       │  Sequence.FromString()           [[Phases/Parsing Sequences|Parsing Sequences]]
       ▼
   Sequence (BaseEvent[])
       │
       │  PlacementCalculator             [[Phases/Calculating the Placement|Calculating the Placement]]
       ▼
   Placement[]   (sample-index timeline)
       │
       │  SampleProcessor + IResampler    [[Phases/Loading Into Memory|Loading Into Memory]]
       ▼              ↑
   ProcessedEvent[]   │ raw samples come from
       │              │ SampleHolder       [[Phases/Getting All Samples|Getting All Samples]]
       │
       │  PCMEncoder (channels × chunks)  [[Phases/Encoding|Encoding]]
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

1. [[Phases/Getting All Samples|Getting All Samples]] — Loading `sounds.json`, downloading every sound, and discovering custom user samples. Owned by `SampleHolder`.
2. [[Phases/Loading Into Memory|Loading Into Memory]] — Decoding every `.wav` from disk into `PcmDataHolder` / `AudioData<float>`. Covers `WaveDecoder`.
3. [[Phases/Parsing Sequences|Parsing Sequences]] — Turning the raw `|`-delimited text into a `Sequence` of `BaseEvent`s, including `#define` macros, `#icut`, `!bg`, `!pulse`, etc.
4. [[Phases/Calculating the Placement|Calculating the Placement]] — Walking the sequence as a tiny VM (BPM, transpose, volume, jumps, loops, cuts) to produce a flat `Placement[]` indexed in audio samples.
5. [[Phases/Encoding|Encoding]] — Resampling each unique sound, then mixing every placement into the final stereo buffer using SIMD-vectorized chunks across multiple worker threads. Covers `PCMEncoder`, `AudioMixer`, `ProcessedEvent`, `RenderedSequence`, `TimedEvent`.

## Front-ends

Each of these is a thin shell around the same `PcmEncoder` library:

- [[Converter.CLI/Converter.CLI|Converter.CLI]] — `dotnet run`, `-i input.🗿 -o output.wav`. Runs encoder, writes file.
- [[Converter.GUI/Converter.GUI|Converter.GUI]] — Avalonia + ReactiveUI desktop app. Provides a settings window, progress bar, log view.
- [[Discord Bot/Discord Bot|Discord Bot]] — DSharpPlus slash bot. Right-click a TDW message attachment → "TDW to OGG/MP3", server pipes encoder output through `ffmpeg`.

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
│       ├── TimedEvents.cs
│       ├── PercentageScale.cs
│       └── ObjectExtensions.cs
│
├── ThirtyDollarConverter.Parser/           (sequence text → events)
│   ├── Sequence.cs                         ← FromString() lives here
│   ├── BaseEvent.cs / NormalEvent.cs
│   ├── Sound.cs / ValueScale.cs
│   └── Custom Events/
│       ├── ExtendedEvent.cs                (+ pan, + offset)
│       ├── IndividualCutEvent.cs           (#icut)
│       ├── BookmarkEvent.cs / EndEvent.cs
│       └── ICustomActionEvent / ICustomAudibleEvent / IHiddenEvent
│
├── ThirtyDollarConverter.Audio/            (PCM containers, mixers, resamplers)
│   ├── PCM/
│   │   ├── AudioData.cs                    ← per-channel float[][] container
│   │   ├── AudioMixer.cs                   ← multi-track mixer
│   │   ├── PcmDataHolder.cs                ← raw decoded WAV
│   │   ├── AudioLayout.cs / Encoding.cs / Int24.cs
│   │   └── DataHolderExtensions.cs / Int24Extensions.cs
│   ├── Mixers/
│   │   ├── IMixingMethod.cs
│   │   └── BasicMixer.cs                   (current default — straight sum)
│   ├── Resamplers/
│   │   ├── IResampler.cs
│   │   ├── HannSincResampler.cs            (default in core)
│   │   ├── HermiteResampler.cs             (default in CLI)
│   │   ├── KaiserBest / KaiserFast / KaiserSinc
│   │   ├── LinearResampler.cs / NoInterpolationResampler.cs
│   │   └── ByteCruncherResampler.cs
│   └── Wave/
│       └── WaveDecoder.cs                  (RIFF / RF64 reader)
│
├── ThirtyDollarConverter.CLI/              → [[Converter.CLI/Converter.CLI|Converter.CLI]]
├── ThirtyDollarConverter.GUI/              → [[Converter.GUI/Converter.GUI|Converter.GUI]]
├── ThirtyDollarConverter.DiscordBot/       → [[Discord Bot/Discord Bot|Discord Bot]]
├── ThirtyDollarConverter.Tests/            (xUnit)
└── ThirtyDollarConverter.Next/             (experimental — undocumented)
```

## Recommended reading order

If you are new to the project, read the phases linearly:

1. [[Phases/Getting All Samples|Getting All Samples]]
2. [[Phases/Loading Into Memory|Loading Into Memory]]
3. [[Phases/Parsing Sequences|Parsing Sequences]]
4. [[Phases/Calculating the Placement|Calculating the Placement]]
5. [[Phases/Encoding|Encoding]]

Then skim whichever front-end you actually plan to use ([[Converter.CLI/Converter.CLI|CLI]], [[Converter.GUI/Converter.GUI|GUI]] or [[Discord Bot/Discord Bot|Bot]]).
