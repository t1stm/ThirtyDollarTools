# Converter.GUI

> Owning project: `Converter/ThirtyDollarConverter.GUI/`

A small Avalonia + ReactiveUI desktop app that exposes the converter pipeline through a graphical UI. Same five phases as the [CLI](../Converter.CLI/Converter.CLI.md) ([Getting All Samples](../Phases/1%20-%20Getting%20All%20Samples.md) → [Loading Into Memory](../Phases/2%20-%20Loading%20Into%20Memory.md) → [Parsing Sequences](../Phases/3%20-%20Parsing%20Sequences.md) → [Calculating the Placement](../Phases/4%20-%20Calculating%20the%20Placement.md) → [Encoding](../Phases/5%20-%20Encoding.md)) — just with a window around them.

## Layout

| Folder | Purpose |
| --- | --- |
| `Views/` | Avalonia `.axaml` + code-behind for each window (`MainWindow`, `Greeter`, `Downloader`, `ExportSettings`). |
| `ViewModels/` | ReactiveUI view-models. `MainWindowViewModel` is the orchestrator; `ExportSettingsViewModel` is the form bound to `EncoderSettings`; `DownloaderViewModel` shows download progress for [Phase 1](../Phases/1%20-%20Getting%20All%20Samples.md). |
| `Services/`  | `DialogService` for file pickers; `ResamplerService` for the resampler dropdown. |
| `Models/`    | `ResamplerModel` — display-name + factory for each `IResampler` implementation. |
| `Behaviors/` | Avalonia behaviors (e.g. auto-scroll for the log pane). |
| `Helpers/`   | Misc UI helpers. |

## Flow

1. **Greeter** — first-run window. Triggers `LoadSampleList` and shows the `Downloader` if any `.wav` files are missing.
2. **Downloader** — runs `DownloadSamples()` with a progress bar wired through `SampleHolder.DownloadUpdate`.
3. **MainWindow** — drag-and-drop a sequence, pick `ExportSettings` (sample rate, channels, resampler, normalization, cut delay, …), encode.
4. The encode runs on a background task; progress is surfaced via the `PcmEncoder.OnProgressUpdate`/`OnLog` events into the log pane.
5. The output `.wav` is written via `WriteAsWavFile` and revealed in the platform's file manager.

## Relationship to the rest of the converter

The GUI is a thin Avalonia shell over the same `PcmEncoder` that the [CLI](../Converter.CLI/Converter.CLI.md) and [Discord Bot](../Discord%20Bot/Discord%20Bot.md) use. All the heavy lifting lives in the [Converter](../Converter.md) core; the GUI just exposes the knobs from `EncoderSettings` as data-bound form fields.

---

**Up:** [Converter](../Converter.md)
