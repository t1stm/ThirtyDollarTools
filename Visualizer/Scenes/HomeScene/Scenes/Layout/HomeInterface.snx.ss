// The home screen's palette. Deliberately not the loader's - the two used to share the
// drifting gradients and looked like the same screen. The three accents are the first
// three entries of the editor's sound palette (Scenes/Styles/Theme.snx.ss), so a colour
// means the same thing here as it does over a note: identity, not decoration. The lit
// step colour is the editor's playhead colour, for the same reason.

// ---------------------------------------------------------------- chrome

id stage {
    width = 100%;
    height = 100%;
    background = "#0e0f16";
}

// Holds the masthead and the band as one vertically centred group. Its padding is the
// page margin every line on this screen is measured from.
id page {
    width = 100%;
    height = 100%;
    padding = 64;
    direction = "vertical";
    vertical-align = "center";
    spacing = 44;
}

// ---------------------------------------------------------------- masthead

id masthead {
    width = 100%;
    height = auto;
    direction = "horizontal";
    vertical-align = "center";
    spacing = 26;
}

// A fixed box the head bounces inside rather than a flex child: a flex parent rewrites
// its children's y on every layout pass, which is every frame here. The extra height over
// the image is the headroom the hop needs - the bounce travels height/4.27 upwards, so 88
// of image wants 21 above it.
id moai-slot {
    width = 88;
    height = 112;
}

// y is the rest line the bounce is measured from, and the head sits on it: the hop only
// ever goes up, so all the slack in the slot belongs above.
id moai {
    x = 0;
    y = 8;
    width = 88;
    height = 88;
}

id masthead-text {
    width = auto;
    height = auto;
    direction = "vertical";
    spacing = 10;
}

// Tracked by hand: the sheet has no letter-spacing, and the spaces are what separate the
// platform ("Thirty Dollar", shared with the Converter and the website) from this tool.
id eyebrow {
    font-size = 13;
    font-color = "#565f89";
}

id wordmark {
    font-size = 60;
    font-color = "#e4e8f5";
}

// ---------------------------------------------------------------- the band
//
// A plain panel, not a flex: the cells are placed by percentage so the playhead can be a
// sibling that moves across them without the flex reserving space for it.

id band {
    width = 100%;
    height = 200;
    background = "#171925";
}

class cell {
    height = 100%;
    width = 33.34%;
    background = "#171925";
    cursor = "pointer";

    state[hovered] = {
        background = "#20233a";
    }
}

id cell-visualizer { x = 0%; }
id cell-drum-master { x = 33.33%; }
id cell-editor { x = 66.67%; }

class divider {
    width = 1;
    height = 100%;
    background = "#2b2e45";
}

id divider-left { x = 33.33%; }
id divider-right { x = 66.67%; }

// ---------------------------------------------------------------- cell contents
//
// Positioned by hand against the cell's top-left, so the three text baselines line up
// across all three cells regardless of how long a description runs.

class steps {
    x = 32;
    y = 32;
    width = auto;
    height = 10;
    direction = "horizontal";
    spacing = 6;
}

class step {
    width = 10;
    height = 10;
    border-radius = 2;
}

class step-blue { background = "#4c6bcc66"; }
class step-orange { background = "#c7784066"; }
class step-green { background = "#3d997566"; }

// Set on a step while the head is over it or just past it. Appended by SetClass, so it
// wins over the resting colour above. Two of them because the band is read twice at
// different volumes: `lit` is the arrival, `lit-idle` is the loop that never stops after
// it, dim enough to be movement in the corner of the eye rather than something to watch.
class lit { background = "#c0caf5"; }
class lit-idle { background = "#c0caf5cc"; }

class cell-title {
    x = 32;
    y = 104;
    font-size = 26;
    font-color = "#d6dadc";
}

class cell-desc {
    x = 32;
    y = 146;
    font-size = 14;
    font-color = "#8b93b8";
}

// ---------------------------------------------------------------- playhead
//
// Hidden until HomeInterface sweeps it across the band once, on entry. The outer panel is
// the glow and carries the x the sweep sets; the line is its child so it paints on top.

id playhead {
    width = 48;
    height = 100%;
    visible = false;
    background = !gradient {
        type = "linear";
        direction = 90deg;
        stops = !stops [
            0% = "#c0caf500",
            50% = "#c0caf526",
            100% = "#c0caf500"
        ];
    };
}

id playhead-line {
    x = 23;
    width = 2;
    height = 100%;
    background = "#c0caf5";
}

// ---------------------------------------------------------------- footer

id footer {
    x = 32;
    y = 100%;
    anchor-y = "end";
    width = auto;
    height = auto;
    padding = 32;
    direction = "horizontal";
    vertical-align = "center";
    spacing = 16;
}

id version-label {
    font-size = 13;
    font-color = "#565f89";
}

id update-label {
    font-size = 13;
    font-color = "#565f89";
}

// The update note only when there's something to act on.
class note-attention { font-color = "#e0af68"; }

// ---------------------------------------------------------------- settings

id topbar {
    width = 100%;
    height = auto;
    padding = 32;
    direction = "horizontal";
    horizontal-align = "end";
}

component button {
    background = "#1c1f2e";
    border-radius = 8;
    width = 104;
    height = 36;
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
