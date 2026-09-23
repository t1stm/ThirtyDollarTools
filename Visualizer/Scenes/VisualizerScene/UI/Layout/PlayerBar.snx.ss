// The player bar borrows the home screen's palette and its playhead (HomeInterface.snx.ss),
// so the line that swept the band on the way in is the same line marking where the cover
// is now. PlayerBar fades the whole tree through ElementAlpha, which scales against the
// alphas set here - a translucent colour stays translucent at full fade.

// ---------------------------------------------------------------- chrome

// The cover sets the scene's background, bright yellow as often as not. Opaque, so the
// controls read the same over any of them - even 5% of a yellow shows through as olive.
id bar-root {
    width = 100%;
    height = 64;
    y = 100%;
    anchor-y = "end";
    background = "#0e0f16";
}

// ---------------------------------------------------------------- timeline
//
// Full width so a click lands close to the moment it means. Only 4 tall; PlayerBar
// widens the seek target past it.

id progress-bar {
    x = 0;
    y = 0;
    width = 100%;
    height = 4;
}

component progress {
    background = "#2b2e45";
    foreground = "#c0caf5";
    cursor = "pointer";
}

// The outer panel is the glow and carries the x PlayerBar sets; the line and stem are its
// children so they paint on top. Home's shape, except the line runs dim below a bright
// stem: at full strength it would strike through the clock whenever the cover is early on.
// (A vertical gradient would do this in one panel, but linear gradients only run
// left to right.)
id playhead {
    y = 0;
    width = 40;
    height = 100%;
    background = !gradient {
        type = "linear";
        direction = 90deg;
        stops = !stops [
            0% = "#c0caf500",
            50% = "#c0caf51f",
            100% = "#c0caf500"
        ];
    };
}

id playhead-line {
    x = 19;
    width = 2;
    height = 100%;
    background = "#c0caf559";
}

id playhead-stem {
    x = 19;
    width = 2;
    height = 16;
    background = "#c0caf5";
}

// ---------------------------------------------------------------- controls

// Below the timeline, so their row is the bar's height minus it.
id left-group {
    x = 12;
    y = 4;
    height = 60;
    direction = "horizontal";
    vertical-align = "center";
    spacing = 8;
}

id right-group {
    x = 100%;
    y = 4;
    anchor-x = "end";
    height = 60;
    padding = 12;
    direction = "horizontal";
    vertical-align = "center";
}

// Fixed width, so "Play" and "Pause" swap without shifting the clock beside them.
component button {
    background = "#1c1f2e";
    border-radius = 6;
    width = 84;
    height = 32;
    font-size = 14;

    state[hovered] = {
        background = "#262a3d";
    }

    state[pressed] = {
        background = "#171925";
    }
}

component label {
    font-size = 14;
    font-color = "#d6dadc";
}

// The clock: where the cover is, bright; how long it runs, dim. Its padding is what sets
// it apart from the buttons.
id clock {
    height = auto;
    padding = 12;
    direction = "horizontal";
    vertical-align = "center";
    spacing = 6;
}

id current-time {
    font-size = 15;
    font-color = "#e4e8f5";
}

id time-separator {
    font-size = 15;
    font-color = "#565f89";
}

id total-time {
    font-size = 15;
    font-color = "#8b93b8";
}
