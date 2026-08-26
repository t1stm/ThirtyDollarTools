# Faithful track — e2e bug log

End-to-end pass driven through the running app (headless X11, `visualizer-headless`) on
August 26 2026, against the working tree at `3d97646` + uncommitted changes. Flow exercised:
add a faithful track, build two instruments (one single-sound, one layered), press them into
the sequence, add actions, remove slots, drag to reorder, scroll to adjust, select/delete/undo,
divider, place as a clip, play, save, load.

Numbering is stable — new findings append, they don't renumber.

---

## 1. Sequence draws nothing while the inspector counts items — **FIXED**

**Severity:** blocker. This is the one Kris hit ("all sounds disappear despite the track info
showing that there are 11 items").

**Symptom:** with anything under one full line (16 slots) in the sequence, the Sequence panel
is empty. The inspector shows the real Items/Length, playback works, save writes the right
file — only the drawing is gone. Surviving a view swap and a reopen, so not a stale queue.

**Repro:** new faithful track → one instrument → click it once. Nothing appears.

**Root cause:** `LayoutHandler.Height` only moves on `NewLine()` (`Reset()` sets it to 0,
`NewLine` sets it to `Y`). Two places derived a chunk's bottom bound from it:

- `ChunkGenerator.PositionChunk` — `chunk.EndY = LayoutHandler.Height + LayoutHandler.Size`
- `EventCanvas.MeasureAll` — the same expression

`StartY` comes from `LayoutHandler.Y`, which is absolute. So for a block that never wraps,
a chunk got `StartY = 439, EndY = 51` — an inverted bound in the wrong coordinate space.
`EventCanvas.VisibleChunks` then culled every chunk (`EndY + LineHeight < clip.Y`) and
nothing was ever queued for render.

Why it looked fine when it was built: the palettes each fill an exact line, and a sequence
of 16+ slots wraps, so both take the `NewLine` path where `Height == Y`.

**Fix:** both sites now take the bottom from `Y`, which is absolute either way and identical
to the old value in the wrapping case. Regression check in
`EditorScene.Tests/PlayfieldLayoutTests.cs` pins the `Height`/`Y` invariant.

**Also latent in the visualizer** — `PositionChunk` is the playfield's own path, so a
sequence shorter than one line would have been culled there too.

---

## 2. Editor keybinds fire through modal file dialogs — **FIXED**

**Severity:** real, easy to hit.

**Repro:** open Load (or Save) → type any path containing `m`, `s`, `d`, `p`… → the letters
run the editor's shortcuts behind the modal. Typing `/tmp/tdviz` toggled channel 1's Mute
(`m`) and reset the transport.

**Cause:** `Editor.KeyDown` runs `_context.DispatchKeyDown(e)` first and returns if a focused
element ate the key; otherwise it falls straight into the `Keybinds.Match` table with no
"is a modal open" gate. The file dialog has no focused `TextInput`, so every key falls
through. Dialogs that *do* focus an input (the action-value dialog) are fine by accident.

**Fix:** `DialogHost.HasOpenModal` (any `ModalLayer` mounted on the root), surfaced as
`EditorInterface.HasOpenModal`, and one `if (…) return;` in `Editor.KeyDown` ahead of the
bind table — so `FaithfulKeyDown`'s Tab/Enter/Delete are gated too. Escape's fallthrough
chain still sits ahead of the guard, and focused inputs still eat their keys before it.

---

## 3. File dialog rows are only clickable on the text glyphs — **FIXED**

**Severity:** real; makes Load look completely broken.

**Repro:** open Load → click anywhere on a directory row that isn't directly over the
letters → nothing. Clicking on the text itself navigates fine.

**Cause:** `FileSelection.RefreshFiles` builds each row as a bare `Label`, which measures to
its own text, so the row's hit area is only as wide as the name. `Assets/` is ~50 px of a
~530 px-wide list.

**Fix:** both row labels in `RefreshFiles` now set `Width = Percent(100)` after `FontSizePx`
(the remeasure order that bit `EditorTrack`), so the whole list width is the hit area. No
stylesheet is involved here, so no `ApplyStyleSheet` re-apply is needed.

**Knock-on:** Load has no filename field, only the browser, so until a row is clicked
correctly there is no way to reach a directory at all — you can only walk upward with `Up`.
Save is survivable because its name field takes a full path.

---

## 4. `!divider` persists with a trailing newline — **FIXED**

**Severity:** cosmetic / data hygiene.

A saved faithful track stores `"action": "!divider\n"`. `NormalEvent.Stringify` deliberately
appends `\n` for `!divider` because that is what a divider means in exported TDW text, but
`ProjectIO` reuses the same `Stringify()` to persist an item's action, so the newline lands
in the JSON. It round-trips correctly (`Sequence.FromString` re-parses it), so this is only
untidiness — but the same `Stringify()` also prefills the right-click edit dialog, so any
future value-carrying event with formatting of its own would leak it into the input box too.

**Fix:** dropped the `!divider` case from `NormalEvent.Stringify` entirely. Nothing wanted
it: .tdw text goes through `SequenceText.Serialize`, which builds its own strings (and joins
on `"|\n"` anyway); the three live callers — `ProjectFile`, the inspector row and the value
dialog — all want the bare `!divider`. `base.Stringify()` still carries a value (`!divider@3`).

---

## 5. Undo clears the whole item selection — **FIXED**

**Severity:** UX nit, deliberate in code.

`EditorState.Undo`/`Redo` call `ClearSelection()`. After undoing a Delete the restored slot
comes back unselected, so a following Enter ("place another") silently does nothing — it
looks like Enter is broken.

**Fix:** `ClearSelection()` in `Undo`/`Redo` became `PruneSelection()`, which only drops
selected items/notes/placements the step took out of the project — so a selection an undo
doesn't touch survives it. `DeleteSelection`'s three undo lambdas reselect what they put
back, which is what the prune had to make room for (the old clear ran after the lambda and
wiped it). Redo needs nothing: the prune drops the re-deleted objects on its own.
`EditorStateTests.Undo_RestoresTheDeletedSelection_AndKeepsSurvivingOnes` pins all three.

---

## 6. Switching between two faithful tracks leaves the old sequence on screen — **FIXED**

**Severity:** blocker for a project with more than one faithful track.

**Symptom:** open faithful track A, then open faithful track B. Everything else follows —
the header name, the inspector's Items/Length, playback, and the bounces, which land on
B's schedule — but the Sequence panel still draws A's slots.

**Repro:** `~/tdw/amalgam/Amalgam.tdwproj` with the drum tracks converted to Faithful;
open `drums #1`, then open `drums #2`.

**Root cause:** `EditorInterface.SwapGridView` early-returns on `next == _openPanel`, and
faithful → faithful keeps the same panel, so `_faithfulSequence.Refresh()` never ran. It is
the only view that caches: the note editor reads `State.OpenedTrack` on every layout pass
(track → track switching was always fine), while the sequence holds its own expanded event
stream and play schedule.

**Fix:** refresh the sequence inside the same-panel branch. Rebuilding the palette and
`CenterOnZero` stay on the real panel change — the palette is project-wide, not per track.

---

## Verified working

Everything else in the flow behaved: palette insertion, layered instruments expanding to
`!combine`-joined slots, group hover lighting, Draw-click removal (a layered item removes as
one unit), drag-to-reorder for both single and layered items, scroll-to-adjust with the value
badge and the shorter feedback bounce, right-click value editing (`!speed@150` → `!speed@600`,
length 2.8 s → 0.7 s), Tab dividers with the site's double line break, Enter duplication,
Select/Delete/Undo, clip placement with the walked duration as its width, playback, and a
save → load round trip that came back byte-for-byte identical on screen.

## Tooling note (not an app bug)

`viz.sh dblclick` cannot trigger a double click: `press()` does a `mousemove` + `sleep 0.2`
before each `mousedown`, so the gap between the two presses is ~430 ms against
`UIContext.DoubleClickMs = 400`. Drive it with a single `xdotool` chain instead.
