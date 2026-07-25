# Converter.GUI

> Owning project: `Converter/ThirtyDollarConverter.GUI/`

A small Avalonia + ReactiveUI desktop app that exposes the converter pipeline through a graphical UI. Same five phases as the [[../Converter.CLI/Converter.CLI|CLI]] ([[../Phases/1 - Getting All Samples|Getting All Samples]] → [[../Phases/2 - Loading Into Memory|Loading Into Memory]] → [[../Phases/3 - Parsing Sequences|Parsing Sequences]] → [[../Phases/4 - Calculating the Placement|Calculating the Placement]] → [[../Phases/5 - Encoding|Encoding]]) — just with a window around them.

## Layout

| Folder | Purpose |
| --- | --- |
| `Views/` | Avalonia `.axaml` + code-behind for each window (`MainWindow`, `Greeter`, `Downloader`, `ExportSettings`). |
| `ViewModels/` | ReactiveUI view-models. `MainWindowViewModel` is the orchestrator; `ExportSettingsViewModel` is the form bound to `EncoderSettings`; `DownloaderViewModel` shows download progress for [[../Phases/1 - Getting All Samples|Phase 1]]. |
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

The GUI is a thin Avalonia shell over the same `PcmEncoder` that the [[../Converter.CLI/Converter.CLI|CLI]] and [[../Discord Bot/Discord Bot|Discord Bot]] use. All the heavy lifting lives in the [[../Converter|Converter]] core; the GUI just exposes the knobs from `EncoderSettings` as data-bound form fields.

---

**Up:** [[../Converter|Converter]]
