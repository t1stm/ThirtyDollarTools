// The settings screen continues the home screen: same stage colour, same page margin,
// same eyebrow-over-wordmark masthead - home reads THIRTY DOLLAR / VISUALIZER, this one
// reads VISUALIZER / SETTINGS, so the hierarchy nests instead of restarting. It used to
// be a translucent rounded card floating over the loader's drifting gradients, which is
// the screen home deliberately stopped looking like.
//
// The only saturated colour on the page is in the preview line at the top: the three
// tile hues are the editor's sound palette, the same three home puts under its tool
// names. Everything else is the chrome, so the eye lands on the thing being configured.

// ---------------------------------------------------------------- chrome

id stage {
    width = 100%;
    height = 100%;
    background = "#0e0f16";
}

// 64 is home's page margin. The list is the only child with a percentage height, so the
// vertical flex hands it whatever the masthead and the preview line don't take.
id page {
    width = 100%;
    height = 100%;
    padding = 64;
    direction = "vertical";
    spacing = 32;
}

// ---------------------------------------------------------------- masthead

id masthead {
    width = 100%;
    height = auto;
    direction = "vertical";
    spacing = 8;
}

// Tracked by hand with spaces - the sheet has no letter-spacing.
id eyebrow {
    font-size = 13;
    font-color = "#565f89";
}

id wordmark {
    font-size = 44;
    font-color = "#e4e8f5";
}

// ---------------------------------------------------------------- preview line
//
// A playfield line's worth of events, at the size, gap and count the three geometry
// settings describe. It wraps rather than scaling down to fit: a tile is always the pixel
// size the playfield would draw, which is the only reason to show it at all - shrinking
// them to fit the width meant raising the event size barely changed what you saw.
//
// It is the page's hero because those settings are unreadable as numbers: "event margin
// 12" means nothing until you see the gap it makes.

// The bed, at a fixed height on purpose. Sizing it to the tiles meant the whole page
// shifted under the pointer while a slider was being dragged - the one moment nothing
// should move. It scrolls instead, so a 64-event line at full size is reachable without
// the settings below it going anywhere.
id strip-view {
    width = 100%;
    height = 300;
    background = "#171925";
}

id strip {
    width = 100%;
    height = auto;
    padding = 24;
    direction = "horizontal";
    wrap = true;
}

class tile { border-radius = 2; }

// The editor's first three sound colours, cycled: a real line is a mix of sounds, not a
// run of one.
class tile-blue { background = "#4c6bcc"; }
class tile-orange { background = "#c77840"; }
class tile-green { background = "#3d9975"; }

// ---------------------------------------------------------------- the list

id settings-list {
    width = 100%;
    height = 100%;
    spacing = 40;
}

// A group is what a setting touches, which is also when it takes effect - the playfield
// group is live, the window group waits for a restart. Ordered that way: live first.
class section {
    width = 100%;
    height = auto;
    direction = "vertical";
    spacing = 18;
}

class section-header {
    width = 100%;
    height = auto;
    direction = "vertical";
    spacing = 10;
}

class section-title {
    font-size = 13;
    font-color = "#565f89";
}

class rule {
    width = 100%;
    height = 1;
    background = "#2b2e45";
}

// ---------------------------------------------------------------- a row
//
// Name over description on the left at a fixed measure, control on the right. The fixed
// measure is what puts every control on one vertical line without a grid.

class row {
    width = 100%;
    height = auto;
    direction = "horizontal";
    vertical-align = "center";
    spacing = 32;
}

class row-text {
    width = 280;
    height = auto;
    direction = "vertical";
    spacing = 4;
}

class setting-name {
    font-size = 15;
    font-color = "#d6dadc";
}

class setting-desc {
    font-size = 13;
    font-color = "#8b93b8";
}

class control {
    width = auto;
    height = auto;
    direction = "horizontal";
    vertical-align = "center";
    spacing = 16;
}

// Fixed width and end-aligned so the readout doesn't shuffle as digits are gained.
class value {
    width = 56;
    height = auto;
    direction = "horizontal";
    horizontal-align = "end";
}

class value-label {
    font-size = 15;
    font-color = "#c0caf5";
}

// Filled in home's playhead colour: the same near-white blue means "where the value is"
// on both screens.
class setting-slider {
    width = 240;
    height = 12;
    border-radius = 6;
    background = "#21243a";
    foreground = "#c0caf5";
}

// The resampler picker: a row control that steps through a closed list of names, so it
// is sized like a keybind button rather than a slider - "Sinc (Kaiser best)" has to fit
// without the row reflowing when it becomes "Hermite".

class choice-button {
    width = 240;
    height = 32;
    background = "#21243a";
    border-radius = 6;
    font-size = 14;

    state[hovered] = {
        background = "#2b2f4a";
    }

    state[pressed] = {
        background = "#171925";
    }
}

// ---------------------------------------------------------------- shortcuts
//
// A shortcut row is a settings row with a button where the control goes, so it reuses
// everything above and only the button is new. Wider than the sliders because the label
// is the binding itself - "Ctrl+Shift+Z" has to fit without the row reflowing when it
// becomes "Already used by ..." mid-capture.

class keybind-button {
    width = 240;
    height = 32;
    background = "#21243a";
    border-radius = 6;
    font-size = 14;

    state[hovered] = {
        background = "#2b2f4a";
    }

    state[pressed] = {
        background = "#171925";
    }
}

// Waiting for a key. Home's playhead blue, the same colour the sliders fill with - it
// means "this is the live one" on both.
class keybind-button-capturing {
    background = "#3a4270";
}

// Refused: the combo is already somebody else's on this screen.
class keybind-button-conflict {
    background = "#5c3140";
}

// Quieter than a keybind button and at the end of the section, so it reads as the way
// out rather than another row to set.
class reset-shortcuts {
    width = 160;
    height = 32;
    background = "#1c1f2e";
    border-radius = 6;
    font-size = 13;

    state[hovered] = {
        background = "#262a3d";
    }
}

// ---------------------------------------------------------------- back

// Top right, where home puts the button that opens this screen - the same corner takes
// you both ways.
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

component text-input {
    background = "#171925";
    border-radius = 4;
    height = 32;
}

component label {
    font-size = 15;
    font-color = "#d6dadc";
}
