# Converter.CLI

> Owning project: `Converter/ThirtyDollarConverter.CLI/`

A small command-line front-end for the converter pipeline. Wraps the same five-phase pipeline ([Getting All Samples](../Phases/1%20-%20Getting%20All%20Samples.md) → [Loading Into Memory](../Phases/2%20-%20Loading%20Into%20Memory.md) → [Parsing Sequences](../Phases/3%20-%20Parsing%20Sequences.md) → [Calculating the Placement](../Phases/4%20-%20Calculating%20the%20Placement.md) → [Encoding](../Phases/5%20-%20Encoding.md)) that the [GUI](../Converter.GUI/Converter.GUI.md) and [Discord Bot](../Discord%20Bot/Discord%20Bot.md) use, just driven by command-line flags instead of a UI.

## Files

| File | Purpose |
| --- | --- |
| `Program.cs`  | Entry point. Parses options, sets up `SampleHolder`, encodes each input file. |
| `Options.cs`  | `CommandLine`-attributed options class. |
| `Readers.cs`  | Helpers for reading sequence files from disk. |
| `Progressbar.cs` | Tiny console progress-bar used during sample download / encode. |

## Options

```text
-i, --input             (required)  One or more `.🗿` sequence files.
-o, --output                        Output `.wav` paths (paired with --input).
-s, --sample-rate                   Override the encoder sample rate.
    --download-location             Override the samples cache directory.
```

## Pipeline

For each input file the CLI:

1. Calls `SampleHolder.LoadSampleList()` + `DownloadSamples()` + `LoadSamplesIntoMemory()` (Phases 1–2).
2. Reads the file with `Sequence.FromString` (Phase 3).
3. Builds a `PcmEncoder` configured with `HermiteResampler` (CLI default) and the requested sample rate, then calls `GetSequenceAudio` (Phases 4–5).
4. Writes the result with `WriteAsWavFile`.

Logging is via Serilog to stdout. There is no incremental render path here — the CLI always produces a fresh full encode.

## Relationship to the rest of the converter

The CLI is the thinnest possible wrapper around the [Converter](../Converter.md) core. If something works in the CLI but not in the GUI/Bot, the bug is almost always in a UI front-end and not in the encoder.

---

**Up:** [Converter](../Converter.md)
