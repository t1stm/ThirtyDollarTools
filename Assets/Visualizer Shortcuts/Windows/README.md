# Windows shortcuts

Start Menu shortcuts for the visualizer, plus two file types:

| Extension | Opens with | Does |
| --- | --- | --- |
| `.tdwproj` | `--mode editor -i "%1"` | Opens the project in the editor. |
| `.tdw`, `.moai`, `.🗿` | `--mode visualizer -i "%1"` | Loads the sequence in the visualizer, which starts playing it straight away. |

`.lnk` files are binary and store an absolute path to the exe, so there is nothing
useful to check in - `Install-Shortcuts.ps1` writes them against wherever the app
actually lives.

## Install

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-Shortcuts.ps1 "C:\path\to\folder\with\ThirtyDollarVisualizer.exe"
```

With no argument it looks for the exe next to the script first, then in the repo's
`Visualizer\ThirtyDollarVisualizer\bin\Release\net10.0`. No admin rights needed -
everything goes under `HKCU` and `%APPDATA%`.

| What | Where | Does |
| --- | --- | --- |
| `Thirty Dollar Visualizer.lnk` | `%APPDATA%\...\Start Menu\Programs` | Starts the app normally. |
| `Thirty Dollar Editor.lnk` | same | Starts it with `--mode editor`. |
| `ThirtyDollarVisualizer.Project` | `HKCU\Software\Classes` | The `.tdwproj` type. |
| `ThirtyDollarVisualizer.Sequence` | same | The `.tdw` / `.moai` / `.🗿` type. |
| `thirty-dollar-visualizer.ico` | copied next to the exe | The icon the shortcuts and both types point at. |

The icon is copied beside the exe on purpose: shortcuts and `DefaultIcon` hold absolute
paths, so pointing them into this folder would break the moment the repo moved.

The script ends with `SHChangeNotify(SHCNE_ASSOCCHANGED)`, so Explorer picks the new
types up without a sign-out.

## Check it worked

```powershell
cmd /c assoc .tdwproj                                    # .tdwproj=ThirtyDollarVisualizer.Project
cmd /c assoc .tdw                                        # .tdw=ThirtyDollarVisualizer.Sequence
cmd /c ftype ThirtyDollarVisualizer.Sequence             # ..."<exe>" --mode visualizer -i "%1"
Start-Process some-song.tdw                              # visualizer, playing
```

If Windows still offers you a "How do you want to open this?" picker, pick the app once
and tick "Always" - a per-user choice already made in Explorer outranks a freshly
registered file type.

## Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-Shortcuts.ps1 -Uninstall
```

The icon copied next to the exe is left behind; delete it by hand if you care.

## Notes

- **Not run on Windows.** It was written and parse-checked on Linux (`Parser::ParseFile`
  clean, the `SHChangeNotify` P/Invoke compiles, the `.🗿` surrogate pair builds back to
  U+1F5FF), but the registry writes, `WScript.Shell` and the Explorer refresh have not
  been exercised. Worth one careful run.
- **A throwaway Wine prefix got partway and no further.** The registry side checks out
  there: `.tdw`, `.moai` and `.🗿` all round-trip as extension keys pointing at the
  ProgID, and the `"<exe>" --mode visualizer -i "%1"` command string comes back byte for
  byte - so the astral-plane extension really is a Windows-only win. Nothing past that
  is testable under Wine: PowerShell 7.2 and 7.4 both start, initialise AMSI and exit 0
  without running a line (not even `-Help` prints), `wscript` is equally silent, and
  Wine's `assoc` only reads `HKCR` - it doesn't merge `HKCU\Software\Classes` the way
  Windows does, so the association can't be fired end to end either.
- **`.🗿` works here, unlike on Linux.** The registry is UTF-16, so the extension key is
  just a surrogate pair; the freedesktop glob matcher Linux uses stops at U+FFFF and
  can't match it at all. The extension is built with `[char]0xD83D``[char]0xDDFF` rather
  than pasted in, so the script survives being read as ANSI. It is saved UTF-8 with a
  BOM for the same reason - Windows PowerShell 5.1 assumes ANSI without one.
- The shortcuts set `WorkingDirectory` to the app's folder, and the open commands inherit
  it: `Settings.30$` and `Editor Backups\` are written relative to the working directory,
  and a Start Menu launch starts somewhere else entirely.
- Rebuilding the app is enough; re-run the script only if its folder moves.
