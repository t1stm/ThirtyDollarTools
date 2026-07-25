# Thirty Dollar Tools

Welcome to the **Thirty Dollar Tools** vault. This is the umbrella project containing all of the tooling that revolves around the [Thirty Dollar Website](https://thirtydollar.website) save file format (TDW sequences). It is a multi-project .NET solution implemented primarily in C#.

The vault is split into top-level sections, each containing its own index and child documents. Click into the relevant area to read on.

## Sections

- [[Converter/Converter|Converter]] — The TDW → WAV/audio conversion pipeline. Library + GUI + CLI + Discord Bot.
- [[Sundex Engine/Sundex Engine|Sundex Engine]] — The in-house C# / .NET 10 game engine used by the Visualizer. Engine, Components, Markup pipeline, Style DSL.
- [[Visualizer/Visualizer|Visualizer]] — *(documentation pending)* the renderer / live cover playback application.

## Repository layout (high-level)

```
ThirtyDollarTools/
├── Converter/                  → see [[Converter/Converter|Converter]]
│   ├── ThirtyDollarConverter           (core encoding library)
│   ├── ThirtyDollarConverter.Audio     (PCM containers, mixers, resamplers, WAV I/O)
│   ├── ThirtyDollarConverter.Parser    (TDW sequence text parser + event types)
│   ├── ThirtyDollarConverter.CLI       (command-line front-end)
│   ├── ThirtyDollarConverter.GUI       (Avalonia-based desktop GUI)
│   ├── ThirtyDollarConverter.DiscordBot(Discord bot front-end)
│   ├── ThirtyDollarConverter.Tests
│   ├── ThirtyDollarConverter.Debug / .Migrate / .EngineerSounds
│   └── ThirtyDollarConverter.Next      (experimental rewrite — not documented yet)
├── Sundex/                     → see [[Sundex Engine/Sundex Engine|Sundex Engine]]
├── Visualizer/                 → see [[Visualizer/Visualizer|Visualizer]]
└── BMS2TDWex/                  (BMS → TDW conversion helper)
```

## Where to start reading

If you want to understand how a `.🗿` sequence ends up as a `.wav`, start at [[Converter/Converter|Converter]] and follow the **Phases** chapter end-to-end.
