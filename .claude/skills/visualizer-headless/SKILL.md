---
name: visualizer-headless
description: Run the Debug Visualizer on a private headless X11 display and drive it - click, type, drag, screenshot - without a window ever appearing on the user's desktop. Use whenever the app has to actually run: verifying a GUI or scene change, reproducing a bug, checking the Editor/Home/Settings layout, or when the user asks to see the app, screenshot it, or watch it live.
---

# Visualizer, headless

`viz.sh` (next to this file) runs the Debug build on its own Xvfb display and drives it
with xdotool. Screenshots come back as PNGs you read with the Read tool; `start` also
brings up a VNC server on that display, so the user can attach a viewer and watch it
live at any time without asking for it separately.

For an e2e pass rather than a one-off look, read `UI-MAP.md` next to this file first: what
each scene and panel holds, which surfaces are worth testing, where the model-side oracles
are, and the driving traps (chiefly: `dblclick` below cannot actually double-click).

**Never start this app any other way.** GLFW prefers Wayland whenever the compositor is
reachable, so a plain `dotnet run` - even with `DISPLAY` set, even with `WAYLAND_DISPLAY`
unset - opens a real window on the user's desktop. `XDG_SESSION_TYPE=x11` is what makes
GLFW pick X11 and honour `DISPLAY`; the script sets it.

## Use it

```bash
S=.claude/skills/visualizer-headless
bash $S/viz.sh start            # builds Debug, starts Xvfb :99, boots to Home (~20 s)
bash $S/viz.sh shot home        # -> /tmp/tdviz/shots/home.png   (Read that path)
bash $S/viz.sh click 1290 495   # the Editor card on Home
bash $S/viz.sh key ctrl+s
bash $S/viz.sh stop             # kills app, VNC and Xvfb
```

Commands: `start [app args]`, `restart`, `stop`, `status`, `shot [name]`,
`click X Y [button]`, `dblclick X Y`, `drag X1 Y1 X2 Y2 [button]`, `key <keys>`,
`type <text>`, `scroll up|down [n]`, `log [lines]`, `vnc`.

`start` passes anything after it to the app, so `start --mode editor -i cover.tdw`
works. The window is at 0,0, so screen coordinates are window coordinates: take a
`shot`, read the pixel position off the image, click it.

## Letting the user watch

`start` already brings up VNC and prints a `vncviewer localhost:5900` line - hand that
to the user when they want to see it live or take the mouse themselves. Otherwise send
screenshots. `vnc` is only needed to reprint the line or to bring VNC back after killing
it separately; it's a no-op if VNC is already running.

## Worth knowing

- **Clicks are held ~150 ms on purpose.** Sundex samples pointer state once a frame and
  fires clicks on release, so a plain `xdotool click` can fall between two frames and do
  nothing. Use the script's `click`, never raw xdotool. Keys are fine either way.
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
- Env: `VIZ_DISPLAY` (`:99`), `VIZ_SIZE` (`1600x900`), `VIZ_DIR` (`/tmp/tdviz`),
  `VIZ_VNC_PORT` (`5900`), `VIZ_NO_BUILD=1` to skip the build.
- `VIZ_SIZE` needs the RandR mode fix the script applies: Xvfb advertises a fixed
  1280x1024 output whatever `-screen` says and X confines the pointer to it, so without
  it nothing past x=1279 is clickable.
