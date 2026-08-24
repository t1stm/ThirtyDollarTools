# Linux shortcuts

Desktop entries for the visualizer, plus two file types:

| File | Opens with | Does |
| --- | --- | --- |
| `.tdwproj` | `--mode editor -i <file>` | Opens the project in the editor. |
| `.tdw`, `.moai`, `.🗿` | `--mode visualizer -i <file>` | Loads the sequence in the visualizer, which starts playing it straight away. |

## Install

```bash
./install.sh /path/to/folder/with/ThirtyDollarVisualizer
```

With no argument it looks for the binary next to `install.sh` first, then in the repo's
`Visualizer/ThirtyDollarVisualizer/bin/Release/net10.0`. Everything lands under
`~/.local/share`, so no root and nothing outside your user.

| File | Goes to | Does |
| --- | --- | --- |
| `thirty-dollar-visualizer.desktop` | `~/.local/share/applications` | The menu entry. Right-click it for **Open the editor**, which passes `--mode editor`. |
| `thirty-dollar-editor.desktop` | `~/.local/share/applications` | Handles `.tdwproj`. Hidden from the menu - see below. |
| `thirty-dollar-sequence.desktop` | `~/.local/share/applications` | Handles sequences. Hidden from the menu too. |
| `thirty-dollar-visualizer.xml` | `~/.local/share/mime/packages` | Defines `application/x-tdwproj` and `application/x-tdw`. |
| `thirty-dollar-visualizer.png` | `~/.local/share/icons/hicolor/256x256/apps` | The icon the entries name. |

The script then runs `update-mime-database`, `update-desktop-database` and
`xdg-mime default`, so the associations are live straight away - no logout.

## Check it worked

```bash
xdg-mime query filetype some-project.tdwproj   # -> application/x-tdwproj
xdg-mime query filetype some-song.tdw          # -> application/x-tdw
xdg-mime query default application/x-tdw       # -> thirty-dollar-sequence.desktop
gio open some-song.tdw                         # visualizer, playing
```

## Uninstall

```bash
./install.sh --uninstall
```

## Notes

- **`.🗿` does not auto-detect here, and can't.** The glob is declared and lands in
  `~/.local/share/mime/globs` correctly, but the freedesktop glob matcher only handles
  characters up to U+FFFF, and 🗿 is U+1F5FF. Verified by probing: `*.mö` (U+00F6) and
  `*.→` (U+2192) both match, while `*.🗿` and `*.𝄞` (U+1D11E) both fall through to
  `text/plain`. Rename to `.tdw`, or right-click → **Open With** for that one file. The
  glob stays declared so it starts working if the matcher ever grows past the BMP. The
  Windows installer has no such limit and registers `.🗿` properly.
- **Two hidden entries, not one visible one.** `Exec=` can't hold a shell snippet
  (reserved characters; `desktop-file-validate` rejects it), and an entry ending in
  `-i %f` breaks when it's launched from the menu with no file - `-i` with nothing after
  it is a parse error. So each file handler is its own `NoDisplay=true` entry.
- **`Path=` matters.** The app writes `Settings.30$` and `Editor Backups/` relative to
  the working directory. A menu launch starts in your home folder, so the entries set
  `Path=` to the app's own folder to keep them where a terminal run puts them.
- **Rebuild, don't reinstall.** The entries point at the binary in place; rebuilding is
  enough. Re-run `install.sh` only if the folder moves.
- The two types are declared subclasses of `application/json` and `text/plain`
  respectively, which is what they are. Text editors stay available in "Open With",
  below these entries.
