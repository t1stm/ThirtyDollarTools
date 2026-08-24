#!/usr/bin/env bash
# Drives the Debug Visualizer on a private Xvfb display. See SKILL.md.
set -euo pipefail

here=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
repo=$(cd -- "$here/../../.." && pwd)
proj="$repo/Visualizer/ThirtyDollarVisualizer/ThirtyDollarVisualizer.csproj"
app_dir="$repo/Visualizer/ThirtyDollarVisualizer/bin/Debug/net10.0"

disp=${VIZ_DISPLAY:-:99}
size=${VIZ_SIZE:-1600x900}
state=${VIZ_DIR:-/tmp/tdviz}
log="$state/visualizer.log"
settings="$state/Settings.30\$"
pidfile="$state/app.pid"

# pipefail would make a dead display abort the script, hence the || true.
win() { DISPLAY=$disp xdotool search --name "Thirty Dollar Visualizer" 2>/dev/null | head -1 || true; }
alive() { [ -f "$pidfile" ] && kill -0 "$(cat "$pidfile")" 2>/dev/null; }

ensure_display() {
    mkdir -p "$state/shots"
    DISPLAY=$disp xdpyinfo >/dev/null 2>&1 && return
    Xvfb "$disp" -screen 0 "${size}x24" >"$state/xvfb.log" 2>&1 </dev/null &
    echo $! >"$state/xvfb.pid"
    for _ in $(seq 40); do DISPLAY=$disp xdpyinfo >/dev/null 2>&1 && break; sleep 0.25; done
    # Xvfb advertises one 1280x1024 RandR output no matter what -screen says, and X
    # confines the pointer to it: without a matching mode, anything past x=1279 is
    # unclickable - the pointer silently stops short.
    local w=${size%x*} h=${size#*x} mode name
    mode=$(cvt "$w" "$h" | tail -1 | cut -d' ' -f2- | tr -d '"')
    name=${mode%% *}
    DISPLAY=$disp xrandr --newmode $mode 2>/dev/null || true
    DISPLAY=$disp xrandr --addmode screen "$name" 2>/dev/null || true
    DISPLAY=$disp xrandr --output screen --mode "$name" 2>/dev/null || true
}

cmd_start() {
    ensure_display
    if [ -z "${VIZ_NO_BUILD:-}" ]; then
        dotnet build "$proj" -c Debug --nologo -v q >"$state/build.log" 2>&1 ||
            { echo "build failed:"; tail -20 "$state/build.log"; exit 1; }
    fi
    if [ -n "$(win)" ]; then echo "already running on $disp (window $(win))"; exit 0; fi

    # Its own settings file, so a headless run never touches the dev one - and seeded
    # past UpdateCheckAsked, which is what puts the first-run setup wizard on screen.
    [ -f "$settings" ] || printf '%s\n' '# headless runs - skips the first-run setup' \
        'UpdateCheckAsked = True' 'CheckForUpdates = False' >"$settings"

    local audio=(--no-audio)
    [ -n "${VIZ_AUDIO:-}" ] && audio=()

    # XDG_SESSION_TYPE=x11 is the load-bearing bit: GLFW otherwise picks Wayland and
    # the window opens on the user's real desktop, DISPLAY be damned.
    cd "$app_dir"
    XDG_SESSION_TYPE=x11 DISPLAY=$disp LIBGL_ALWAYS_SOFTWARE=1 \
        nohup ./ThirtyDollarVisualizer --settings-location "$settings" "${audio[@]}" "$@" \
        >"$log" 2>&1 </dev/null &
    echo $! >"$pidfile"

    for _ in $(seq 120); do [ -n "$(win)" ] && break; sleep 0.5; alive || break; done
    [ -n "$(win)" ] || { echo "no window appeared:"; tail -20 "$log"; exit 1; }
    for _ in $(seq 120); do grep -q "Transitioning to scene: Home" "$log" && break; sleep 0.5; done
    sleep 2  # the loader fades out into Home, and input is ignored until it lands
    cmd_status
}

# By pid file, never pkill -f: the patterns would match any shell whose command line
# happens to mention them - including the caller's.
cmd_stop() {
    local f p
    for f in "$pidfile" "$state/vnc.pid" "$state/xvfb.pid"; do
        p=$(cat "$f" 2>/dev/null) || continue
        kill "$p" 2>/dev/null || true
        rm -f "$f"
    done
    echo "stopped $disp"
}

cmd_status() {
    local w; w=$(win)
    [ -n "$w" ] || { echo "not running on $disp - start it with: $0 start"; return 0; }
    echo "display $disp  app pid $(cat "$pidfile" 2>/dev/null || echo none)  window ${w:-none}"
    [ -n "$w" ] && DISPLAY=$disp xdotool getwindowgeometry "$w"
    echo "log: $log"
    tail -1 "$log" 2>/dev/null || true
}

cmd_shot() {
    local w f; w=$(win)
    f="$state/shots/${1:-shot-$(date +%H%M%S)}.png"
    DISPLAY=$disp import -window "${w:-root}" "$f"
    echo "$f"
}

# Sundex samples pointer state once a frame and fires clicks on release, so a default
# 12 ms xdotool click can fall between two frames and never register. Hold the button.
press() {
    DISPLAY=$disp xdotool mousemove "$1" "$2"; sleep 0.2
    DISPLAY=$disp xdotool mousedown "${3:-1}"; sleep 0.15
    DISPLAY=$disp xdotool mouseup "${3:-1}"
}

cmd_click() { press "$@"; sleep 0.4; echo "clicked $1,$2"; }
cmd_dblclick() { press "$1" "$2" "${3:-1}"; sleep 0.08; press "$1" "$2" "${3:-1}"; echo "double-clicked $1,$2"; }

cmd_drag() {
    local b=${5:-1}
    DISPLAY=$disp xdotool mousemove "$1" "$2"; sleep 0.2
    DISPLAY=$disp xdotool mousedown "$b"; sleep 0.15
    # Stepped, so drag handlers that follow the pointer see motion rather than a jump.
    for i in 1 2 3 4; do
        DISPLAY=$disp xdotool mousemove $(( $1 + (($3 - $1) * i) / 4 )) $(( $2 + (($4 - $2) * i) / 4 ))
        sleep 0.05
    done
    sleep 0.1; DISPLAY=$disp xdotool mouseup "$b"; echo "dragged $1,$2 -> $3,$4"
}

cmd_key() { DISPLAY=$disp xdotool windowfocus "$(win)" key --clearmodifiers "$@"; sleep 0.3; }
cmd_type() { DISPLAY=$disp xdotool windowfocus "$(win)" type --delay 30 "$*"; sleep 0.3; }
cmd_scroll() {
    local b=4; [ "${1:-up}" = down ] && b=5
    DISPLAY=$disp xdotool click --repeat "${2:-3}" --delay 60 $b
}

cmd_log() { tail -n "${1:-40}" "$log"; }

cmd_vnc() {
    ensure_display
    # x11vnc refuses to start if it smells a Wayland session, hence the env scrub.
    env -u WAYLAND_DISPLAY XDG_SESSION_TYPE=x11 x11vnc -display "$disp" -localhost \
        -nopw -forever -shared -rfbport "${VIZ_VNC_PORT:-5900}" >"$state/vnc.log" 2>&1 </dev/null &
    echo $! >"$state/vnc.pid"
    sleep 2
    echo "watch it live: vncviewer localhost:${VIZ_VNC_PORT:-5900}  (stop with '$0 stop')"
}

case "${1:-}" in
    start|stop|status|shot|click|dblclick|drag|key|type|scroll|log|vnc) c=$1; shift; "cmd_$c" "$@" ;;
    restart) shift; cmd_stop; cmd_start "$@" ;;
    *) sed -n '/^case /,/^esac/p' "$0" | head -3; echo "see SKILL.md"; exit 1 ;;
esac
