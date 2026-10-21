---
name: visualizer-headless
description: Run the Debug Visualizer on a private headless X11 display and drive it - click, type, drag, screenshot - without a window ever appearing on the user's desktop. Use whenever the app has to actually run: verifying a GUI or scene change, reproducing a bug, checking the Editor/Home/Settings layout, or when the user asks to see the app, screenshot it, or watch it live.
---

# Visualizer, headless

`viz.sh` (next to this file) runs the Debug build on its own private X display and drives
it with xdotool. Screenshots come back as PNGs you read with the Read tool; `start` also
brings up a VNC server on that display, so the user can attach a viewer and watch it
live at any time without asking for it separately.

For an e2e pass rather than a one-off look, read `UI-MAP.md` next to this file first: what
each scene and panel holds, which surfaces are worth testing, where the model-side oracles
are, and the driving traps.

**Never start this app any other way.** GLFW prefers Wayland whenever the compositor is
reachable, so a plain `dotnet run` - even with `DISPLAY` set, even with `WAYLAND_DISPLAY`
unset - opens a real window on the user's desktop. `XDG_SESSION_TYPE=x11` is what makes
GLFW pick X11 and honour `DISPLAY`; the script sets it.

## Use it

```bash
S=.claude/skills/visualizer-headless
bash $S/viz.sh start            # builds Debug, starts display :99, boots to Home (~20 s)
bash $S/viz.sh shot home        # -> /tmp/tdviz/shots/home.png   (Read that path)
bash $S/viz.sh click 1290 495   # the Editor card on Home
bash $S/viz.sh key ctrl+s
bash $S/viz.sh stop             # kills app, VNC and Xvfb
```

Commands: `start [app args]`, `restart`, `stop`, `status`, `shot [name]`,
`move X Y`, `click X Y [button]`, `dblclick X Y`, `drag X1 Y1 X2 Y2 [button]`, `key <keys>`,
`type <text>`, `scroll up|down [n]`, `log [lines]`, `wait <regex> [seconds]`, `vnc`.

`start` passes anything after it to the app, so `start --mode editor -i cover.tdw`
works. The window is at 0,0 and fills the display (`-w`/`-h` from `VIZ_SIZE`), so
screen coordinates are window coordinates: take a `shot`, read the pixel position off the
image, click it. It runs capped at 60 fps (`-f 60`). Passing your own `-w`, `-h` or `-f`
replaces the script's value, so `start -f 0` runs uncapped.

## Wait on the log, not on sleep

The input commands never sleep. A Debug build traces input once a frame has handled it
(`Game.LogInput`, `[hh:mm:ss DBG Input] Pointer at|pressed|released ...`, `Key pressed|released`,
`Text input`, `Scrolled`), and each step waits for its line before the next: `click` moves,
waits for the frame to see the pointer there, presses, waits, releases, waits - about 60 ms
in all. `key` holds each key until a frame has seen it down and then up, `type` returns once
every character has landed, `scroll` sends one notch per frame. The trace and the editor's
step lines below are `[Conditional("DEBUG")]`, so a Release build has none and every step
falls back to a 3 s timeout - `viz.sh` builds Debug for this reason.

`start` returns once the app logs `[Boot] Handed off to <scene>`, whatever `--mode` is.
After that, `wait <regex> [seconds]` (default 10) blocks until a log line matching the
regex appears *after the last click/key/type/drag/scroll*, prints it, and fails on timeout.
The frame that shows the change is drawn right after the line, so `shot` straight after
a `wait` catches it:

```bash
bash $S/viz.sh click 126 50; bash $S/viz.sh wait 'Dialog opened'; bash $S/viz.sh shot
```

The editor's lines are `[hh:mm:ss INF Editor] ...`, all in
`Visualizer/Scenes/EditorScene/Logs.cs`: `Dialog opened|closed: <id>` (every modal, by the
id of its markup root, e.g. `track-type-dialog`, `file-dialog`), `Track added|opened|closed|
selected|removed|converted`, `Note added`, `Item added`, `Clip placed`, `Pasted`, `Deleted`,
`Undo`, `Redo`, `Tool:`, `Project loaded|saved`, `Imported`, `Exported`, and
`Render finished` (the background re-render that backs the "Rendering audio..." status). A
timeout after an input usually means the input missed - but it can also be a true
negative, such as clicking an action that takes no value and waiting for a dialog.

## Text benchmark

`--mode text-bench` fills the window with text and times its draw on the GPU (the Slug
plan's 4 ms gate, `docs/handover/sundex-slug-plan.md`):

```bash
bash $S/viz.sh stop; VIZ_SIZE=1920x1080 bash $S/viz.sh start --mode text-bench
grep '\[TextBench\]' /tmp/tdviz/visualizer.log   # one line per variant, then verdict=pass|fail
```

Repeat at `VIZ_SIZE=1080x720` (the minimum window) for context. It takes ~40 s and closes
itself. `start` may say "not running" once the benchmark has closed itself; that's
expected. Wait for the `verdict=` line instead. The app runs under `nohup`, so its exit code
(1 on a fail) never reaches you; the verdict line carries it. Before you grep, make sure
the log is from this run and not the last one. Anything else using the GPU at the same time
(a game, a miner) inflates the numbers.

## Letting the user watch

`start` already brings up VNC and prints a `vncviewer localhost:5900` line - hand that
to the user when they want to see it live or take the mouse themselves. Otherwise send
screenshots. `vnc` is only needed to reprint the line or to bring VNC back after killing
it separately; it's a no-op if VNC is already running.

## Worth knowing

- **It renders on the GPU.** The display is a rootful Xwayland on a private headless
  weston (`weston --backend=headless --renderer=gl`), so the app gets DRI3 and radeonsi,
  the same driver as a desktop run. That makes it about 25x faster than Xvfb's llvmpipe
  (Home: ~1950 fps against ~77, uncapped with `-f 0`). `status` prints the renderer.
  `VIZ_GPU=0` goes back to Xvfb + llvmpipe, and so does a machine without weston or
  Xwayland. Zink and VirtualGL on Xvfb were tried and dropped: zink never gets pixels onto
  Xvfb (black screenshots), and VirtualGL's per-frame readback caps it near 400 fps and
  crashes under `mangohud --dlsym`.
- **Never drive input with raw xdotool.** Sundex samples pointer state once a frame and fires
  clicks on release, so a plain `xdotool click` can fall between two frames and do nothing;
  a key tap can do the same to a scene that polls `KeyboardState` (`IsPressed` in
  `Visualizer.Keyboard`). The script's commands hold each button and key until the input
  trace says a frame saw it, which is both safe and faster than any fixed hold.
- **Headless runs get their own `Settings.30$`** in `/tmp/tdviz`, seeded past
  `UpdateCheckAsked` so the first-run setup wizard stays out of the way. The dev settings
  in `bin/Debug` are never touched. Delete `/tmp/tdviz/Settings.30$` to see the wizard.
- Runs with `--no-audio`; set `VIZ_AUDIO=1` to keep audio.
- Sounds are read from `bin/Debug/net10.0/Sounds`, same as a normal dev run. If they are
  missing the loader downloads them on first boot, which needs network and time.
- Everything logs to `/tmp/tdviz/visualizer.log` - `log 100` after a click that did
  nothing, exceptions land there.
- The app dies with the shell that started it (no `setsid`), so a `start` in one Bash
  call and a `click` in the next is fine, but a killed session takes the app with it.
- Env: `VIZ_DISPLAY` (`:99`), `VIZ_SIZE` (`1600x840`), `VIZ_DIR` (`/tmp/tdviz`),
  `VIZ_VNC_PORT` (`5900`), `VIZ_NO_BUILD=1` to skip the build, `VIZ_GPU=0` for Xvfb.
- `start` reuses whatever X server already answers on `VIZ_DISPLAY`, so changing
  `VIZ_SIZE` or `VIZ_GPU` needs a `stop` first.
- In Xvfb mode, `VIZ_SIZE` needs the RandR mode fix the script applies: Xvfb advertises a fixed
  1280x1024 output whatever `-screen` says and X confines the pointer to it, so without
  it nothing past x=1279 is clickable.
