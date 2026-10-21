#!/usr/bin/env bash
# Drives the Debug Visualizer on a private Xvfb display. See SKILL.md.
set -euo pipefail

here=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
repo=$(cd -- "$here/../../.." && pwd)
proj="$repo/Visualizer/ThirtyDollarVisualizer/ThirtyDollarVisualizer.csproj"
app_dir="$repo/Visualizer/ThirtyDollarVisualizer/bin/Debug/net10.0"

disp=${VIZ_DISPLAY:-:99}
size=${VIZ_SIZE:-1600x840}  # the display and the app window; UI-MAP.md is measured at 1600x840
state=${VIZ_DIR:-/tmp/tdviz}
log="$state/visualizer.log"
settings="$state/Settings.30\$"
pidfile="$state/app.pid"
# GPU by default: a rootful Xwayland on a private headless weston is a real X display with
# DRI3, so the app renders on the GPU. VIZ_GPU=0 falls back to Xvfb + llvmpipe.
gpu=${VIZ_GPU:-1}
{ command -v weston && command -v Xwayland; } >/dev/null || gpu=0
wl="tdviz-wl${disp#:}"

# pipefail would make a dead display abort the script, hence the || true.
win() { DISPLAY=$disp xdotool search --name "Thirty Dollar Visualizer" 2>/dev/null | head -1 || true; }
alive() { [ -f "$pidfile" ] && kill -0 "$(cat "$pidfile")" 2>/dev/null; }

ensure_display() {
    mkdir -p "$state/shots"
    DISPLAY=$disp xdpyinfo >/dev/null 2>&1 && return
    if [ "$gpu" = 1 ]; then
        env -u DISPLAY weston --backend=headless --renderer=gl --width="${size%x*}" \
            --height="${size#*x}" --socket="$wl" --idle-time=0 >"$state/weston.log" 2>&1 </dev/null &
        echo $! >"$state/weston.pid"
        for _ in $(seq 40); do [ -S "$XDG_RUNTIME_DIR/$wl" ] && break; sleep 0.25; done
        [ -S "$XDG_RUNTIME_DIR/$wl" ] || { echo "weston did not start:"; tail -5 "$state/weston.log"; exit 1; }
        # WAYLAND_DISPLAY must name the private socket: unset, Xwayland connects to the
        # user's compositor and opens a real window on their desktop.
        WAYLAND_DISPLAY=$wl Xwayland "$disp" -geometry "$size" >"$state/xvfb.log" 2>&1 </dev/null &
    else
        Xvfb "$disp" -screen 0 "${size}x24" >"$state/xvfb.log" 2>&1 </dev/null &
    fi
    echo $! >"$state/xvfb.pid"  # the X server, Xvfb or Xwayland
    for _ in $(seq 40); do DISPLAY=$disp xdpyinfo >/dev/null 2>&1 && break; sleep 0.25; done
    [ "$gpu" = 1 ] && return  # -geometry sizes Xwayland's output, no RandR fix needed
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
    cmd_vnc
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
    # Window = VIZ_SIZE, capped at 60 fps, unless the args set their own. Only the missing
    # ones are added: the app's CommandLineParser rejects a repeated option outright.
    local a w=${size%x*} h=${size#*x} f=60
    for a in "$@"; do
        case $a in -w*|--width*) w= ;; -h*|--height*) h= ;; -f*|--fps-limit*) f= ;; esac
    done
    # WAYLAND_DISPLAY is a second guard: even if GLFW went Wayland, it lands on the private weston.
    local gl=(LIBGL_ALWAYS_SOFTWARE=1)
    [ "$gpu" = 1 ] && gl=(WAYLAND_DISPLAY="$wl")

    # XDG_SESSION_TYPE=x11 is the load-bearing bit: GLFW otherwise picks Wayland and
    # the window opens on the user's real desktop, DISPLAY be damned.
    cd "$app_dir"
    # VIZ_WRAP prefixes the launch - "mangohud --dlsym" puts an FPS counter on the window.
    env XDG_SESSION_TYPE=x11 DISPLAY="$disp" "${gl[@]}" \
        nohup ${VIZ_WRAP:-} ./ThirtyDollarVisualizer --settings-location "$settings" "${audio[@]}" \
        ${w:+-w "$w"} ${h:+-h "$h"} ${f:+-f "$f"} "$@" \
        >"$log" 2>&1 </dev/null &
    echo $! >"$pidfile"
    echo 0 >"$state/mark"

    for _ in $(seq 120); do [ -n "$(win)" ] && break; sleep 0.5; alive || break; done
    [ -n "$(win)" ] || { echo "no window appeared:"; tail -20 "$log"; exit 1; }
    # Logged once the loader's fade has finished and the --mode scene (Home by default) takes input.
    cmd_wait '\[Boot\] Handed off to' 60 >/dev/null || true
    cmd_status
}

# By pid file, never pkill -f: the patterns would match any shell whose command line
# happens to mention them - including the caller's.
cmd_stop() {
    local f p
    for f in "$pidfile" "$state/vnc.pid" "$state/xvfb.pid" "$state/weston.pid"; do
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
    grep -m1 -o '"Renderer":"[^"]*"' "$log" 2>/dev/null || true
    tail -1 "$log" 2>/dev/null || true
}

cmd_shot() {
    local w f; w=$(win)
    f="$state/shots/${1:-shot-$(date +%H%M%S)}.png"
    # The input trace logs after SwapBuffers, but Xwayland only shows a swapped frame on the X
    # window once weston's next repaint lands - measured 1-2 frames later, vsync off. Reading
    # it straight after an input can return the frame before.
    # ponytail: fixed 3-frame settle at 60 fps; an app-side Debug capture (glReadPixels after
    # render, on request) would be exact if this ever reads stale.
    sleep 0.05
    DISPLAY=$disp import -window "${w:-root}" "$f"
    echo "$f"
}

# Each input step waits for the app's input trace (Debug builds; Game.LogInput) to say a frame
# has handled it, instead of sleeping long enough to be sure. Sundex samples the pointer once a
# frame and fires clicks on release, so a button let go before any frame saw it down never
# clicks - hence press, wait, release. A step that times out carries on regardless: a button
# or key must never be left held.
step() {
    local from; from=$(size)
    DISPLAY=$disp xdotool "${@:2}"
    wait_past "$from" "$1" 3 >/dev/null || true
}

bname() { case $1 in 1) echo left ;; 2) echo middle ;; 3) echo right ;; esac; }

# Returns once a frame has seen the pointer there, so hover-driven targets (scroll, the X
# cut key) have caught up. A move to where the pointer already is logs nothing; skipped.
move() {
    [ "$(DISPLAY=$disp xdotool getmouselocation | grep -o 'x:[0-9]* y:[0-9]*')" = "x:$1 y:$2" ] && return 0
    step "Pointer at $1, $2\$" mousemove "$1" "$2"
}

press() {
    local b=${3:-1}
    move "$1" "$2"
    step "Pointer pressed: $(bname "$b")" mousedown "$b"
    step "Pointer released: $(bname "$b")" mouseup "$b"
}

cmd_move() { mark; move "$1" "$2"; }
cmd_click() { mark; press "$@"; echo "clicked $1,$2"; }
cmd_dblclick() { mark; press "$1" "$2" "${3:-1}"; press "$1" "$2" "${3:-1}"; echo "double-clicked $1,$2"; }

cmd_drag() {
    local b=${5:-1} i
    mark
    move "$1" "$2"
    step "Pointer pressed: $(bname "$b")" mousedown "$b"
    # Stepped, so drag handlers that follow the pointer see motion rather than a jump.
    for i in 1 2 3 4; do move $(($1 + ($3 - $1) * i / 4)) $(($2 + ($4 - $2) * i / 4)); done
    step "Pointer released: $(bname "$b")" mouseup "$b"
    echo "dragged $1,$2 -> $3,$4"
}

# The OpenTK Keys name the trace logs for an xdotool keysym.
keyname() {
    case $1 in
        Return) echo Enter ;; KP_Enter) echo KeyPadEnter ;; BackSpace) echo Backspace ;;
        [0-9]) echo "D$1" ;; [a-z]) echo "${1^^}" ;;
        Prior | Page_Up) echo PageUp ;; Next | Page_Down) echo PageDown ;;
        ctrl | Control_L) echo LeftControl ;; Control_R) echo RightControl ;;
        shift | Shift_L) echo LeftShift ;; Shift_R) echo RightShift ;;
        alt | Alt_L) echo LeftAlt ;; Alt_R) echo RightAlt ;; super | Super_L) echo LeftSuper ;;
        bracketleft) echo LeftBracket ;; bracketright) echo RightBracket ;; grave) echo GraveAccent ;;
        *) echo "${1^}" ;;
    esac
}

# Sends keydown or keyup for a key or combo, then waits until a frame has seen every part of it.
keystep() {
    local from part; from=$(size)
    DISPLAY=$disp xdotool windowfocus "$(win)" "$1" "$3"
    for part in ${3//+/ }; do wait_past "$from" "Key $2: $(keyname "$part")\$" 3 >/dev/null || true; done
}

# Each key or combo is held until a frame has seen it down, so scenes polling KeyboardState
# catch it too, and released until one has seen it up, so repeating it reads as a new press.
cmd_key() {
    local k
    mark
    for k in "$@"; do
        keystep keydown pressed "$k"
        keystep keyup released "$k"
    done
}

# Text arrives as one trace line per character; returns once they are all in. Newlines and
# tabs type as keys rather than text, so they are not counted.
cmd_type() {
    local text="$*" from end n
    n=${text//[$'\n\t']/}; n=${#n}
    mark; from=$(size); end=$((SECONDS + 5 + n / 20))
    DISPLAY=$disp xdotool windowfocus "$(win)" type "$text"
    until (($(tail -c +$((from + 1)) "$log" | grep -ac 'Text input: ') >= n)); do
        ((SECONDS < end)) || { echo "timed out typing" >&2; return 1; }
        sleep 0.01
    done
}

# One notch at a time, each waiting for its frame, so none merge or go missing.
cmd_scroll() {
    local b=4 i; [ "${1:-up}" = down ] && b=5
    mark
    for ((i = 0; i < ${2:-3}; i++)); do step 'Scrolled ' click $b; done
}

cmd_log() { tail -n "${1:-40}" "$log"; }

# Every input command marks the log's size before acting, so `wait` only sees what came after.
# Bytes rather than lines: tail -c seeks straight there, where tail -n re-reads the GL-noisy log.
size() { stat -c %s "$log" 2>/dev/null || echo 0; }
mark() { size >"$state/mark"; }

# Blocks until a log line past byte $1 matches the ERE $2 and prints it; fails after $3 s.
# Process substitution, not a pipe: under pipefail a grep -m1 that matches still fails the
# pipe once tail takes the SIGPIPE.
wait_past() {
    local end=$((SECONDS + $3))
    while ((SECONDS < end)); do
        grep -m1 -aE "$2" <(tail -c +"$(($1 + 1))" "$log") && return 0
        alive || { echo "app is not running" >&2; return 1; }
        sleep 0.01
    done
    echo "timed out after $3 s waiting for: $2" >&2
    return 1
}

# Waits on a line that came after the last input command (or start); see SKILL.md.
cmd_wait() { wait_past "$(cat "$state/mark" 2>/dev/null || echo 0)" "$1" "${2:-10}"; }

vnc_alive() { [ -f "$state/vnc.pid" ] && kill -0 "$(cat "$state/vnc.pid")" 2>/dev/null; }

cmd_vnc() {
    ensure_display
    vnc_alive && { echo "vnc already running: vncviewer localhost:${VIZ_VNC_PORT:-5900}"; return 0; }
    # x11vnc refuses to start if it smells a Wayland session, hence the env scrub.
    env -u WAYLAND_DISPLAY XDG_SESSION_TYPE=x11 x11vnc -display "$disp" -localhost \
        -nopw -forever -shared -rfbport "${VIZ_VNC_PORT:-5900}" >"$state/vnc.log" 2>&1 </dev/null &
    echo $! >"$state/vnc.pid"
    sleep 2
    echo "watch it live: vncviewer localhost:${VIZ_VNC_PORT:-5900}  (stop with '$0 stop')"
}

case "${1:-}" in
    start|stop|status|shot|move|click|dblclick|drag|key|type|scroll|log|wait|vnc) c=$1; shift; "cmd_$c" "$@" ;;
    restart) shift; cmd_stop; cmd_start "$@" ;;
    *) sed -n '/^case /,/^esac/p' "$0" | head -3; echo "see SKILL.md"; exit 1 ;;
esac
