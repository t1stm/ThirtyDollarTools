# Keyboard Shortcuts

Reference for the keyboard (and modifier+scroll/click) shortcuts in the Visualizer and its Editor scene.

> **macOS**: everything written `Ctrl` below is `Cmd` on a Mac. The defaults are computed per platform
> (`Keybinds.Primary`), so a Mac's first launch already shows Cmd bindings — nothing has to be set up.

> **Rebinding**: every shortcut in the two tables below is rebindable from **Settings → Visualizer
> shortcuts / Editor shortcuts**. Click a binding, press the new combo, and it takes effect immediately —
> no restart. `Escape` cancels the capture; `Delete`/`Backspace` puts the platform default back. A combo
> already used by another action *on the same screen* is refused rather than stolen. **Reset shortcuts**
> at the end of each section puts that screen back to defaults.
>
> Only what you actually change is saved (as one `Keybinds = …` line in `Settings.30$`); everything else
> follows the platform default, so a settings file copied from a Mac to a PC gets Ctrl back.

## Visualizer

| Shortcut | Action |
|---|---|
| `Escape` | Back to Home |
| `Space` | Play / pause |
| `Shift+Space` | Play / pause without the status message |
| `H` | Show/hide the player bar |
| `C` | Cycle the camera follow mode |
| `←` / `→` | Hold to seek. `Shift` is a tenth of a step, `Shift+Ctrl` a hundredth |
| `↑` / `↓` | Hold to raise/lower the volume |
| `PageUp` / `PageDown` | Jump to the previous / next sequence |
| `R` | Restart the cover |
| `Shift+R` | Restart and stay stopped |
| `Ctrl+Shift+R` | Re-read the loaded files from disk |
| `Ctrl+=` / `Ctrl+-` | Hold to zoom in / out |
| `Ctrl+D` | Toggle the debug overlay |
| `Ctrl+Scroll` | Zoom |
| Scroll | Move the camera |

### Bookmarks (not rebindable)

Thirty combos generated from a loop over the digit row, so they are fixed — only the modifier follows
the platform (`Cmd+1` on a Mac).

| Shortcut | Action |
|---|---|
| `0`–`9` | Seek to that bookmark |
| `Ctrl+0`–`9` | Set that bookmark to the current time |
| `Ctrl+Shift+0`–`9` | Clear that bookmark |

## Editor — common

| Shortcut | Action |
|---|---|
| `Space` | Play / pause playback |
| `Shift+Space` | Restart from the beginning |
| `Ctrl+Z` | Undo |
| `Ctrl+Shift+Z` / `Ctrl+Y` | Redo (two separate, separately rebindable actions) |
| `Ctrl+C` / `Ctrl+V` / `Ctrl+X` | Copy / paste / cut the selection |
| `Ctrl+A` | Select all (in the focused view) |
| `Delete` / `Backspace` | Delete the selected notes/placements (two separate actions) |

## Editor — tools

| Shortcut | Action |
|---|---|
| `D` | Switch to the Draw tool |
| `E` | Switch to the Select tool |

Modifiers are matched exactly, so plain `D` does not fire on `Ctrl+D` — which stays free for a future
"duplicate selection".

## Escape (not rebindable)

`Escape` in the editor isn't a shortcut but a fallthrough chain, and rebinding the head of a chain means
nothing. First matching step wins:

clear selection → close the top modal → close the opened track → back to Home

## Mouse gestures (not rebindable)

Buttons and wheels rather than keys. Their modifier follows the platform primary, so `Cmd` works on a Mac.

| Shortcut | Action |
|---|---|
| `Ctrl+Click` note/clip | Add to selection |
| `Shift+Click` note/clip | Remove from selection |
| `Right-click` note/clip | Delete it |
| Double-click a track | Open it |
| `Right-click` a track | Track options |
| `Ctrl+Scroll` | Zoom, anchored to the pointer |
| `Shift+Scroll` (note editor) | Pan horizontally (time) instead of scrolling the value axis |
| `Shift+Scroll` (arrangement) | Pan horizontally (time) instead of scrolling lanes |
| Scroll (no modifier, arrangement) | Scroll lanes vertically |
| Middle-mouse drag | Pan both axes (FL Studio-style) |
| `Ctrl+Middle-mouse drag` (note editor) | Scale the note/row height, 4–300 px, anchored to the pointer |
| `Shift+drag` | Fine-snap |

## Sound Picker (Instrument editor, adjustments mode)

| Shortcut | Action |
|---|---|
| `Right-click` | Add another copy |
| Scroll | Change value |
| `Ctrl+Shift+Scroll` | Change value by 0.1 |
| `Ctrl+Scroll` | Change volume |
| `Shift+Scroll` | Change pan |

## Text / numeric fields (not rebindable)

Framework-level caret and selection keys in Sundex, not visualizer shortcuts.

| Shortcut | Action |
|---|---|
| `←` / `→` | Move caret (`Shift` extends selection) |
| `Home` / `End` | Caret to start/end (`Shift` extends selection) |
| `Backspace` / `Delete` | Remove character or selection |
| `Ctrl+A` (or `Cmd+A`) | Select all text in the field |
| `Enter` | Commit and blur |
| `Escape` | Blur the field |
| `↑` / `↓` (numeric fields only) | Step value up/down |

Note: a focused text field does not implement its own Ctrl+C/V/X — those combos are deliberately passed
through untouched, and the editor's clipboard shortcuts are suppressed while a text field is focused (so
they don't fire on your typed text).

## Not implemented

For reference, these are *not* currently bound to anything: Ctrl+S (save), Ctrl+O (open), Ctrl+N (new
project), arrow-key nudging of selected notes/placements, Tab focus cycling, F-key shortcuts, mute/solo
hotkeys.
