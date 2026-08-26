# What there is to test — a UI map for e2e passes

`SKILL.md` next to this file covers *how* to drive the app. This covers *what is there*,
what each surface is supposed to do, and the traps that have already cost someone an hour.
Read it before an e2e pass; skim the section for whatever you are touching.

---

## 0. First decide whether you need the app at all

Launching costs a build plus ~20 s of boot, and every assertion after it is a screenshot you
have to read by eye. Most questions don't need it:

| Question | Cheaper answer |
|---|---|
| Does this element exist / have the right class / id? | `EditorScene.Tests` — `EditorInterfaceMarkupTests`, `StyleSelectorTests` |
| Does this handler do the right thing to the model? | `EditorScene.Tests` with `EditorTestContext` (GL is stubbed; see `DrawCallTests` for how a real view is built and drawn in-process) |
| Does the model/export/timing hold? | `ThirtyDollarConverter.Editor.Tests` |
| Does input routing work? | `Sundex.Components.Tests/InputRoutingTests` — drive `UIContext.UpdatePointer` with primitives, not `MouseState` |

Launch the app when the question is **"does the user actually see it"** — layout, culling,
render order, clipping, scroll, or a gesture crossing several elements. That is the class of
bug the test suite structurally cannot catch: the whole faithful sequence once drew nothing
at all while 876 tests passed, because the model was right and only the culling bound was
wrong.

**The technique that finds those:** check the model and the pixels *separately*. The
inspector, the transport length and the saved file are all model-side oracles. If they agree
and the screen doesn't, you have a render bug — go look at culling, clipping and draw order,
not at state.

---

## 1. Getting to a scene

Five scenes, registered under these names: `home`, `visualizer`, `drum-master`, `editor`,
`settings` (`Program.cs` `BuildPreloads`). `start` passes its arguments through:

```bash
S=.claude/skills/visualizer-headless
bash $S/viz.sh start                          # boots to Home
bash $S/viz.sh start --mode editor            # straight into the Editor
bash $S/viz.sh start --mode editor -i cover.tdw   # editor, with that sequence imported
bash $S/viz.sh start --mode visualizer -i cover.tdw
```

`-i` is the visualizer's sequence *unless* `--mode editor`, in which case the editor opens it.
An unknown `--mode` silently falls back to Home and says so in the log.

**Home** (1600×840): three cards on one row — Visualizer ≈ `(308, 497)`, Drum Master
≈ `(800, 497)`, Editor ≈ `(1290, 497)`. Settings button top-right ≈ `(1515, 50)`.

---

## 2. Reading coordinates

The window is at 0,0, so screen coordinates are window coordinates: `shot`, read the pixel
position off the PNG, click it. Everything below is measured at the default `VIZ_SIZE`
(`1600x900`, giving a 1600×840 client area). **Re-measure after any layout change** — treat
the numbers as anchors for orientation, not as constants.

The editor's chrome is pinned to the window edges, so these three hold at any size
(`EditorInterface`):

- header strip: `HeaderHeight = 32` at the top
- track column: `TrackColumnWidth = 260` on the left (inspector column mirrors it on the right)
- hint bar: `HintBarHeight = 26` at the bottom

Everything between them is the view area, and that is where you have to measure.

Useful for reading small on-canvas text (value badges, sequence icons) — crop and upscale
rather than squinting at a full 1600px screenshot:

```bash
python3 -c "
from PIL import Image
Image.open('/tmp/tdviz/shots/x.png').crop((285,425,1100,510)).resize((1630,170)).save('/tmp/tdviz/shots/xc.png')"
```

---

## 3. Editor chrome (present in every view)

| Element | Where (1600×840) | Notes |
|---|---|---|
| `Editor` title, project name, BPM | left of the header, y≈16 | project name updates live; a `*` marks unsaved |
| Load / Save / Export | `(287,16)` / `(340,16)` / `(400,16)` | all three open a modal |
| Track list rows | x 0..250, first row y≈50, ~40 px pitch | colour blip = reorder handle; `×` removes |
| `+ Add track` | below the last row | opens the track-kind modal |
| Transport scrubber | y≈748, `0:00 ─── 0:00` | right-hand number is total project length — a model oracle |
| Play / Stop | `(66,776)` / `(192,776)` | Play becomes Pause while playing |
| Back | `(130,819)` | leaves the editor |
| Draw / Select tools | `(1219,50)` / `(1272,50)` in the grid views; `(1200,404)` / `(1250,404)` in the faithful panel | the faithful panel has its own pair — they are not the same buttons |
| Inspector column | x 1300..1600 | contents depend entirely on the selection, see §7 |
| Status line | bottom-right, `(1317,815)` | `Idle` / `Rendering audio...` — tells you a background render is in flight |
| Hint bar | bottom, y≈827 | **read this first, see below** |

### The hint bar is a free test plan

`EditorInterface.HintLegend` swaps per view and is built from the live bind table, so it
names every gesture that has no on-screen control — which is most of them. The faithful
legend alone lists the palette click, right-click preview, Draw-remove, drag-reorder, the
whole Select-mode keyboard set, copy/paste and the three scroll modifiers. Screenshot it,
read it, and work down it. If a gesture in the legend does nothing, that is a bug by
definition: the app is advertising it.

Hovering a control also writes its own hint there (`OnHint`), which is a cheap way to confirm
a hit area is where you think it is — hover, screenshot the bar, see if it named the thing.

---

## 4. Arrangement view (no track opened)

The default view. Lanes down the left with `M` / `S` per channel, a bar ruler along the top,
clips drawn as coloured blocks.

Worth testing: placing a clip (select a track in the list first, then click a lane — clicking
with nothing selected does nothing and is *not* a bug), dragging and resizing clips,
multi-select and marquee under the Select tool, copy/paste/cut, mute/solo per channel,
double-click a clip to open its track, and clip width tracking a track's real duration
(a faithful clip's width is its walked duration, so editing the sequence should move it).

Gotcha: `M`/`S` are also the keybinds for mute/solo on the selected clips' channels, so a
stray keystroke here changes state. Check the lane header colour before blaming a test step.

---

## 5. Note editor (piano roll track opened)

Opened by double-clicking a Piano Roll track (or adding one — a new track opens itself).

| Element | Where |
|---|---|
| `← Arrangement` | `(328,53)` — back out |
| Track name field | `(511,53)` |
| `Instrument: ▾` | `(685,53)` — the drawing instrument |
| Segment strip | the blue bar at y≈83 |
| Bar ruler | y≈104 |
| Value ruler | x≈277, `+16` down to `-16` |
| Note grid | x from 305 |
| `!cut` row | pinned at the bottom, y≈803 |

Worth testing: drawing notes, dragging them, the value ruler matching where notes land,
segment add/remove from the inspector, per-segment time signature and BPM override, the
`!cut` row (notes there are cuts, and the row has its own history of painting over things),
zoom (`Ctrl`+scroll), pan (middle-drag) and fine-snap (`Shift`+drag).

---

## 6. Faithful panel (faithful track opened)

Three boxed sections. All three draw through one `EventCanvas` at one shared size owned by
`FaithfulScale`, which is why a bug in one often shows in all three.

| Section | Where | What it does |
|---|---|---|
| **Instruments** | top-left card, rows from y≈158 at ~73 px pitch | one row per project instrument, name left, its sounds drawn right. Click a row to append that instrument to the sequence, right-click to preview. `+ New instrument` opens the sound picker |
| **Actions** | top-right card, 3-wide grid from ≈`(1001,157)` | 17 entries. `_pause` is the grey dot at top-left (a silent sound, not an action). Clicking one that takes a value opens the value dialog first |
| **Sequence** | the wide card below, first slot ≈`(318,464)`, 16 per line, wraps | the track's content. Slots shrink to fit 16 across |

Things that are easy to get wrong when reading a screenshot:

- **A layered instrument is several slots but one item.** Two sounds draw as
  `icon • !combine • icon`. Hovering any of them lights all of them; removing or dragging any
  of them moves the whole group. So slot count ≠ item count — the inspector's `Items` is the
  item count.
- **`_pause` draws as a small grey dot**, easily mistaken for an empty slot.
- **`!divider` breaks the line** with a double gap (the site's own behaviour), so the row
  after it starts a new line with a blank line between.

Worth testing, roughly the order that shook bugs out: press instruments in, check `Items` and
`Length` in the inspector, remove slots in Draw mode, drag to reorder (both single and layered),
scroll a slot to change value (`Ctrl`+scroll volume, `Shift`+scroll pan), right-click an action
to reopen its value dialog, `Tab` for a divider, Select mode + `Delete` + `Ctrl+Z`, `Enter` to
place another, place the track as a clip, play, save, load.

**The one to always check:** a sequence with fewer than 16 slots. That is the case that used
to draw nothing at all, and it is the state every new track starts in.

---

## 7. Inspector — what appears when

`InspectorPanel.Sync` picks a section from the selection, so it doubles as a readout of what
the app *thinks* is selected:

| Selection | Header(s) |
|---|---|
| nothing | `Project` (name, author, description, BPM, transpose) |
| a track | `Project` + `Track` (name, colour, project-tempo toggle) + `Track Automation` |
| a faithful track opened, nothing selected | `Faithful track` — name, **Items**, **Length** |
| a faithful sound item | `Sound` (value / volume / pan) |
| a faithful action item | the action's own name, e.g. `!speed` |
| one note | `Note` + `Automation` |
| several notes | `Note (× n)` |
| a `!cut` note | `!cut event` |
| a segment | `Segment` with `+ Add` / `− Remove` |
| clips | `Clips (× n)` + `Track` |

`Items` and `Length` are the cheapest oracle in the app: they come straight off the model, so
"inspector says 11, screen shows 0" localises a bug to rendering in one screenshot.

---

## 8. Dialogs

All of them mount as a `ModalLayer` through `DialogHost` (never a `DropDownLabel` — see the
project memory). `Escape` closes the top one.

| Dialog | Reached by |
|---|---|
| Track type (Piano Roll / Faithful) | `+ Add track` |
| Sound picker / instrument editor | `+ New instrument`, or editing an instrument |
| Action value | clicking a value-taking action; right-clicking a placed one reopens it prefilled |
| Track colour | track context menu → `Change color…` |
| Track context menu | right-click a track row: `Open`, `Change color…`, `Duplicate…`, `Convert to Faithful` / `Convert to Piano Roll`, `Remove` |
| Import | dropping or importing a `.tdw` |
| Export | `Export` |
| Confirm / Unsaved changes / Alert | destructive actions, leaving dirty, and any error |
| File selection | `Load` and `Save` |

**File dialog specifics** (`Sundex.Components/File Selector/FileSelection.cs`): `Up` walks up
one directory, rows navigate or select, `Cancel` / `Select` at the bottom. The **Save** dialog
has a filename field that accepts a full absolute path — use it, it is far faster than
clicking down a tree. The **Load** dialog has no such field, so you must navigate. The list is
directories first, then files filtered by extension; the 📄 prefix renders as a tofu box
(known, `// TODO emojis don't render with new label system`).

Two things here were bugs and are now fixed — if you see them again, it is a regression:
editor keybinds used to fire *through* an open modal (typing a path containing `m` toggled
mute), and file rows used to be clickable only on the text glyphs.

---

## 9. Editor keybinds

From `Keybinds.cs`; all rebindable in Settings, so read the table rather than trusting this
copy if something doesn't match. `Primary` is `Ctrl` (`Cmd` on macOS).

| Keys | Action |
|---|---|
| `Ctrl+Z` / `Ctrl+Shift+Z` / `Ctrl+Y` | undo / redo / redo |
| `Ctrl+C` / `Ctrl+V` / `Ctrl+X` / `Ctrl+A` | copy / paste / cut / select all |
| `Delete`, `Backspace` | delete selection |
| `D` / `E` | Draw tool / Select tool |
| `Space` / `Shift+Space` | play-pause / restart |
| `Ctrl+S` / `Ctrl+O` / `Ctrl+N` | save / open / new |
| arrows | nudge selection (step-or-grid, value-or-lane) |
| `M` / `S` | mute / solo the selected clips' channels |
| `Escape` | fallthrough chain: clear selection → close modal → close track → back |

Faithful-panel-only, handled after the bind table (`EditorInterface.FaithfulKeyDown`):
`Tab` appends a divider, `Enter` places another copy of the selection, `Left`/`Right` walk the
selection, `Ctrl+Shift+` extends it, `Space+` arrow moves the item, `Up`/`Down` adjust its
value.

Note the overlap: `Space` is play-pause globally but a move modifier in faithful Select mode,
and `S` is solo. If a keystroke does something surprising, check the bind table before
assuming the handler is broken.

---

## 10. Driving traps that have already cost time

- **`viz.sh dblclick` cannot double-click.** `press()` does a `mousemove` + `sleep 0.2` before
  each `mousedown`, so the two presses land ~430 ms apart against
  `UIContext.DoubleClickMs = 400`. Use one `xdotool` chain instead:
  ```bash
  DISPLAY=:99 xdotool mousemove X Y sleep 0.2 mousedown 1 sleep 0.1 mouseup 1 \
      sleep 0.06 mousedown 1 sleep 0.1 mouseup 1
  ```
  A "double-click does nothing" finding is almost always this, not the app. Verify with the
  chain before writing it down.
- **Use `viz.sh click`, never a bare `xdotool click`.** Sundex samples the pointer once a
  frame and fires clicks on release; a 12 ms click falls between frames.
- **Scroll goes to whatever is under the pointer**, and `viz.sh scroll` does not move it
  first. `xdotool mousemove X Y`, then `click 4` (up) / `click 5` (down).
- **`--no-audio` is the default.** `VIZ_AUDIO=1` if you are testing playback for real; the
  transport and the playhead work fine without it.
- **`log 100` after any click that did nothing.** Exceptions land in
  `/tmp/tdviz/visualizer.log`; the GL debug output is noisy, so grep rather than tail:
  `grep -aiE "exception|error|unhandled" /tmp/tdviz/visualizer.log | tail`.
- **The app dies with the shell that started it.** A `start` in one Bash call and a `click` in
  the next is fine; a killed session takes the app with it.
- **Headless runs use their own `/tmp/tdviz/Settings.30$`**, seeded past the first-run wizard.
  Delete it to test the wizard.
- **Save test projects to `/tmp/tdviz/`**, not into `bin/Debug` — and clean up anything you do
  put there.

---

## 11. Where findings go

`docs/handover/editor-faithful-bugs.md` is the running log for the faithful track; its
numbering is stable and new findings append. For other areas, start a sibling file rather than
mixing them. Per finding: severity, symptom, minimal repro, root cause with file:line if you
found it, and whether it is fixed. Keep the "verified working" list too — it is what stops the
next agent re-testing the same ground.

If you fix something, leave one runnable check behind it. A pure-logic invariant is usually
enough and far cheaper than a GUI assertion —
`EditorScene.Tests/PlayfieldLayoutTests.cs` pins the `LayoutHandler.Height`/`Y` distinction
that the invisible-sequence bug turned on, in two asserts and no GL.
