# Thirty Dollar Tools

Welcome to the **Thirty Dollar Tools** vault. This is the umbrella project containing all of the tooling that revolves around the [Thirty Dollar Website](https://thirtydollar.website) save file format (TDW sequences). It is a multi-project .NET solution implemented primarily in C#.

The vault is split into top-level sections, each containing its own index and child documents. Click into the relevant area to read on.

## Sections

- [Converter](Converter/Converter.md) — The TDW → WAV/audio conversion pipeline. Library + GUI + CLI + Discord Bot.
- [Sundex Engine](Sundex%20Engine/Sundex%20Engine.md) — The in-house C# / .NET 10 game engine used by the Visualizer. Engine, Components, Markup pipeline, Style DSL.
- Visualizer — *(documentation pending)* the renderer / live cover playback application.

## Repository layout (high-level)

```
ThirtyDollarTools/
├── Converter/                  → see Converter
│   ├── ThirtyDollarConverter           (core encoding library)
│   ├── ThirtyDollarConverter.Encoder   (PCM containers, mixers, resamplers, WAV I/O)
│   ├── ThirtyDollarConverter.Parser    (TDW sequence text parser + event types)
│   ├── ThirtyDollarConverter.Editor    (project-file model: Note, TrackAutomation, SequenceBuilder — not documented yet)
│   ├── ThirtyDollarConverter.CLI       (command-line front-end)
│   ├── ThirtyDollarConverter.GUI       (Avalonia-based desktop GUI)
│   ├── ThirtyDollarConverter.DiscordBot(Discord bot front-end)
│   ├── ThirtyDollarConverter.Tests / .Editor.Tests
│   └── ThirtyDollarConverter.Benchmarks(BenchmarkDotNet)
├── Sundex/                     → see Sundex Engine
├── Visualizer/                 → documentation pending
└── BMS2TDWex/                  (BMS → TDW conversion helper)
```

## Where to start reading

If you want to understand how a `.🗿` sequence ends up as a `.wav`, start at [Converter](Converter/Converter.md) and follow the **Phases** chapter end-to-end.
