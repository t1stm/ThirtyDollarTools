# Editor Keyboard Shortcuts

Reference for all keyboard (and modifier+scroll/click) shortcuts in the Visualizer's Editor scene.

> Note: modifier checks use `Control` only (Left/Right Ctrl) — there is no macOS Cmd alias, so use Ctrl even on Mac builds.

## Common (used constantly)

| Shortcut | Action |
|---|---|
| `Space` | Play / Pause playback |
| `Ctrl+Z` | Undo |
| `Ctrl+Shift+Z` / `Ctrl+Y` | Redo |
| `Ctrl+C` | Copy selection |
| `Ctrl+V` | Paste |
| `Ctrl+X` | Cut selection |
| `Delete` / `Backspace` | Delete selected notes/placements |
| `Escape` | Clear selection → close modal → close track → back to Home (first matching step wins) |

## Selection & Editing

| Shortcut | Action |
|---|---|
| `Ctrl+A` | Select all (in the focused view) |
| `Ctrl+Click` note/clip | Add to selection |
| `Shift+Click` note/clip | Remove from selection |
| `Right-click` note/clip | Delete it |

## Tools

| Shortcut | Action |
|---|---|
| `D` | Switch to Draw tool |
| `E` | Switch to Select tool |

`Ctrl+D` is reserved (unimplemented) for a future "duplicate selection" action — plain `D`/`E` intentionally exclude the Ctrl modifier so they don't collide with it.

## Playback

| Shortcut | Action |
|---|---|
| `Space` | Play / Pause |
| `Shift+Space` | Restart from the beginning |

## View / Navigation

| Shortcut | Action |
|---|---|
| `Ctrl+Scroll` | Zoom, anchored to the pointer |
| `Shift+Scroll` (note editor) | Pan horizontally (time) instead of scrolling the value axis |
| `Shift+Scroll` (arrangement) | Pan horizontally (time) instead of scrolling lanes |
| Scroll (no modifier, arrangement) | Scroll lanes vertically |
| Middle-mouse drag | Pan both axes (FL Studio-style) |
| `Ctrl+Middle-mouse drag` (note editor) | Scale the note/row height, 4-300 px, anchored to the pointer |

## Sound Picker (Instrument editor, adjustments mode)

| Shortcut | Action |
|---|---|
| Scroll | Change value |
| `Ctrl+Scroll` | Change volume |
| `Shift+Scroll` | Change pan |

## Text / Numeric Fields (any text box, e.g. rename, BPM, volume, project name)

| Shortcut | Action |
|---|---|
| `←` / `→` | Move caret (`Shift` extends selection) |
| `Home` / `End` | Caret to start/end (`Shift` extends selection) |
| `Backspace` / `Delete` | Remove character or selection |
| `Ctrl+A` | Select all text in the field |
| `Enter` | Commit and blur |
| `Escape` | Blur the field |
| `↑` / `↓` (numeric fields only) | Step value up/down |

Note: a focused text field does not implement its own Ctrl+C/V/X — those combos are deliberately passed through untouched, and the editor's clipboard shortcuts are suppressed while a text field is focused (so they don't fire on your typed text).

## Not implemented

For reference, these are *not* currently bound to anything: Ctrl+S (save), Ctrl+O (open), Ctrl+N (new project), arrow-key nudging of selected notes/placements, Tab focus cycling, F-key shortcuts, mute/solo hotkeys.
